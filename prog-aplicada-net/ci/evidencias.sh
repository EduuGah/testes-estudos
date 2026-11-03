#!/usr/bin/env bash
# Executa a solução e coleta as evidências dos 8 roteiros (GitHub Actions, ubuntu-24.04).
set -uo pipefail
cd "$(dirname "$0")/.."
EV=evidencias; rm -rf "$EV"; mkdir -p "$EV"
F="dotnet run -c Release --no-build --project ferramentas/HubPedidos.Ferramentas --"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 PATH="$PATH:$HOME/.dotnet/tools"
passo() { echo; echo "######## $1"; }

passo "Ambiente"
{ echo "Data: $(date -u '+%Y-%m-%d %H:%M UTC')"; uname -sr; lscpu | grep -E 'Model name|^CPU\(s\)'; free -h | head -2
  dotnet --version; dotnet --list-runtimes | grep -E 'NETCore|AspNetCore'; } > "$EV/ambiente.txt" 2>&1
cat "$EV/ambiente.txt"

passo "Ferramentas de diagnóstico e EF"
dotnet tool install -g dotnet-ef >/dev/null; dotnet tool install -g dotnet-counters >/dev/null; dotnet tool install -g dotnet-trace >/dev/null
{ dotnet-ef --version | tail -1; dotnet-counters --version; dotnet-trace --version; } > "$EV/ferramentas.txt" 2>&1; cat "$EV/ferramentas.txt"

passo "Restore + build Release"
dotnet restore HubPedidos.sln 2>&1 | tail -3
dotnet build HubPedidos.sln -c Release --no-restore 2>&1 | tee "$EV/build.log" | tail -5

passo "Roteiro 3: migration"
if [ ! -d src/HubPedidos.Infrastructure/Migrations ]; then
  dotnet ef migrations add Inicial -p src/HubPedidos.Infrastructure -s src/HubPedidos.Api -o Migrations 2>&1 | tail -3
  dotnet build HubPedidos.sln -c Release --no-restore 2>&1 | tail -2
fi
dotnet ef migrations script -p src/HubPedidos.Infrastructure -s src/HubPedidos.Api -o "$EV/r3_migration.sql" 2>&1 | tail -2

passo "Roteiro 8: revisão automatizada completa (3 execuções da suíte)"
./revisao.sh > "$EV/r8_revisao.log" 2>&1; echo "revisao.sh saiu com código $?" | tee -a "$EV/r8_revisao.log"
tail -15 "$EV/r8_revisao.log"
for i in 2 3; do
  dotnet test HubPedidos.sln -c Release --no-build 2>&1 | grep -E "Aprovado|Passed|Failed|Com falha|Total" | tail -3 > "$EV/r8_repeticao_$i.txt"
done
cp resultados-testes/resultado.trx "$EV/r8_resultado.trx" 2>/dev/null

passo "Roteiro 1: classificação e catálogo"
$F catalogo > "$EV/r1_catalogo.txt" 2>&1; cat "$EV/r1_catalogo.txt"
passo "Roteiro 3: SQL gerado"
$F sql > "$EV/r3_sql.txt" 2>&1; tail -8 "$EV/r3_sql.txt"
passo "Roteiro 4: experimentos da fila"
$F fila > "$EV/r4_fila.txt" 2>&1; cat "$EV/r4_fila.txt"

passo "Roteiros 2, 3 e 5: BenchmarkDotNet"
dotnet run -c Release --project bench/HubPedidos.Benchmarks -- --filter '*' --artifacts "$EV/bench" > "$EV/bench.log" 2>&1
tail -40 "$EV/bench.log"

passo "Roteiro 5: publicação Release, massa de dados e carga"
dotnet publish src/HubPedidos.Api -c Release -o out/api --no-restore 2>&1 | tail -1
$F semear --db "$PWD/out/hub.db" --n 20000
iniciar_api() {   # $1 = modo do painel, $2 = arquivo de log
  ASPNETCORE_ENVIRONMENT=Laboratorio ConnectionStrings__Hub="Data Source=$PWD/out/hub.db" Hub__ModoPainel="$1" \
    dotnet out/api/HubPedidos.Api.dll --urls http://localhost:5080 > "$2" 2>&1 &
  API=$!
  for _ in $(seq 1 60); do curl -sf http://localhost:5080/api/v1/estatisticas >/dev/null && break; sleep 1; done
  echo "API ($1) no PID $API"
}
parar_api() { kill -INT "$API"; wait "$API" 2>/dev/null; }

medir() {   # $1 = rótulo
  ( sleep 6; dotnet-counters collect -p "$API" --refresh-interval 1 --duration 00:00:00:18 --format csv -o "$EV/r5_counters_$1.csv" \
      --counters System.Runtime,Microsoft.AspNetCore.Hosting,HubPedidos.Fila > /dev/null 2>&1 ) &
  local C=$!
  ( sleep 10; dotnet-trace collect -p "$API" --duration 00:00:00:08 -o "$EV/r5_trace_$1.nettrace" > "$EV/r5_trace_$1.log" 2>&1 ) &
  local T=$!
  $F carga --rota painel --concorrencia 16 --aquecimento 5 --duracao 20 --saida "$EV/r5_carga_$1.json" | tee "$EV/r5_carga_$1.txt"
  wait $C $T
}

iniciar_api Ingenuo "$EV/api_antes.log"
medir antes
parar_api

iniciar_api Otimizado "$EV/api_depois.log"
medir depois
$F carga --rota painel --degraus 4,8,16,32,64 --aquecimento 3 --duracao 8 --saida "$EV/r5_degraus.json" | tee "$EV/r5_degraus.txt"
$F carga --rota criar --concorrencia 8 --aquecimento 3 --duracao 15 --saida "$EV/r4_carga_criar.json" | tee "$EV/r4_carga_criar.txt"
curl -s -H "X-Usuario: carga" http://localhost:5080/api/v1/fila/metricas > "$EV/r4_metricas_api.json"; cat "$EV/r4_metricas_api.json"; echo

passo "Roteiros 6 e 7: HTTP (versões, ETag, cache)"
bash ci/http.sh > "$EV/r67_http.txt" 2>&1; cat "$EV/r67_http.txt" | head -80
curl -s http://localhost:5080/openapi/v1.json > "$EV/r6_openapi_v1.json"
curl -s http://localhost:5080/openapi/v2.json > "$EV/r6_openapi_v2.json"

passo "Roteiro 7: SignalR (autorização, streaming, queda e reconexão)"
$F tempo-real --url http://localhost:5080 > "$EV/r7_tempo_real.txt" 2>&1; cat "$EV/r7_tempo_real.txt"
parar_api

passo "Roteiro 5: traces convertidos"
for t in antes depois; do
  [ -f "$EV/r5_trace_$t.nettrace" ] && dotnet-trace convert "$EV/r5_trace_$t.nettrace" --format Speedscope -o "$EV/r5_trace_$t.speedscope.json" > /dev/null 2>&1
  python3 ci/analisar_trace.py "$EV/r5_trace_$t.speedscope.json" > "$EV/r5_trace_$t.txt" 2>&1; head -25 "$EV/r5_trace_$t.txt"
  rm -f "$EV/r5_trace_$t.nettrace"; gzip -f "$EV/r5_trace_$t.speedscope.json" 2>/dev/null
done

passo "Roteiro 8: a revisão automatizada bloqueia falhas"
for caso in analisador teste; do
  rm -rf /tmp/gate && mkdir /tmp/gate && cp -r . /tmp/gate/ && rm -rf /tmp/gate/evidencias /tmp/gate/out
  if [ $caso = analisador ]; then
    cat > /tmp/gate/src/HubPedidos.Application/ViolacaoProposital.cs <<'CS'
namespace HubPedidos.Application;

public static class ViolacaoProposital
{
    // CA2016: o CancellationToken recebido não é repassado
    public static Task EsperarAsync(CancellationToken ct) => Task.Delay(100);
}
CS
  else
    cat > /tmp/gate/tests/HubPedidos.Tests/FalhaProposital.cs <<'CS'
namespace HubPedidos.Tests;

public class FalhaProposital
{
    [Fact]
    public void Total_do_pedido_esta_errado() => Assert.Equal(74.80m, 2 * 19.90m + 30m);
}
CS
  fi
  /tmp/gate/revisao.sh > "$EV/r8_bloqueio_$caso.log" 2>&1
  echo "revisao.sh saiu com código $?" >> "$EV/r8_bloqueio_$caso.log"
  grep -E "===== \[revisão\]|error|Falha|Failed|falhou|código" "$EV/r8_bloqueio_$caso.log" | head -12
done
echo "fim"
