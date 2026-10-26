#!/usr/bin/env bash
# Executa o Portal de Tarefas de verdade (SQL Server + API + SPA + contêiner) e guarda as evidências em evidencias/.
set -u
cd "$(dirname "$0")/.."
RAIZ=$(pwd)
EV=$RAIZ/evidencias
mkdir -p "$EV/telas"
B=https://localhost:7001
WEB=src/PortalTarefas.Web

# Senhas efêmeras: geradas aqui, nunca gravadas no repositório nem impressas
export SQL_PWD="Sql$(openssl rand -hex 8)!9"
export SEED_PWD="Aa1!$(openssl rand -hex 6)"
echo "::add-mask::$SQL_PWD"; echo "::add-mask::$SEED_PWD"
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__PortalTarefas="Server=localhost,1433;Database=PortalTarefas;User Id=sa;Password=$SQL_PWD;TrustServerCertificate=True"
export SeedIdentidade__Senha="$SEED_PWD"
# Muitos logins seguidos do mesmo IP durante a coleta automatizada (o padrão de produção continua 5/min)
export Portal__LimiteLoginPorMinuto=1000
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

etapa() { echo; echo "================ $* ================"; }
mostra() { echo "\$ $*"; "$@" 2>&1; }
sql() { docker exec sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SQL_PWD" -d PortalTarefas -W -s " | " -Q "$1" 2>&1; }
# curl que imprime o comando (sem cookies nem senhas) e a resposta com cabeçalhos úteis e JSON formatado
req() { local nome=$1; shift
  { echo "\$ curl -i $(printf '%s ' "$@" | sed -E 's/-b [^ ]+ |-c [^ ]+ //g; s/"senha":"[^"]*"/"senha":"***"/g')"
    raw=$(curl -sk -i "$@")
    printf "%s" "$raw" | sed '/^\r\?$/q' | tr -d '\r' | grep -viE "^(date|server|set-cookie|strict-transport|vary):"
    printf "%s" "$raw" | sed '1,/^\r\?$/d' | python3 -m json.tool --no-ensure-ascii 2>/dev/null || printf "%s" "$raw" | sed '1,/^\r\?$/d'
    echo; } >> "$EV/$nome.txt"; }
entrar() { curl -sk -c "/tmp/ck_$1" -H 'Content-Type: application/json' -d "{\"email\":\"$1@portal.local\",\"senha\":\"$SEED_PWD\"}" $B/api/auth/login -o /dev/null -w "%{http_code}"; }
iniciar_api() { (cd $RAIZ && nohup dotnet run --project $WEB --no-build --launch-profile https > "$1" 2>&1 & echo $! > /tmp/api.pid)
  for i in $(seq 1 60); do curl -sk -o /dev/null $B/health/live && return 0; sleep 2; done; echo "API não subiu"; tail -50 "$1"; }
parar_api() { pkill -f "PortalTarefas.Web" ; sleep 3; }

# ---------------------------------------------------------------- ambiente
{
etapa "Versões"
mostra dotnet --version
mostra node --version
mostra npm --version
mostra docker --version
} > "$EV/00_versoes.txt"

docker network create rede >/dev/null
docker run -d --name sql --network rede -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=$SQL_PWD" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest >/dev/null

# ---------------------------------------------------------------- build
{ etapa "Restore e build"; mostra dotnet build PortalTarefas.slnx -c Debug; } > "$EV/01_build.txt"
if ! grep -q "Build succeeded\|Compilação com êxito\|0 Error" "$EV/01_build.txt" || grep -q " error " "$EV/01_build.txt"; then
  echo "BUILD FALHOU"; exit 1
fi

# ---------------------------------------------------------------- Unidade 3: migrations
for i in $(seq 1 30); do docker exec sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SQL_PWD" -Q "SELECT 1" >/dev/null 2>&1 && break; sleep 2; done
{
etapa "Ferramentas locais (dotnet-ef e NSwag)"
mostra dotnet tool restore
etapa "Migration inicial"
if [ ! -d $WEB/Dados/Migrations ]; then mostra dotnet ef migrations add Inicial --project $WEB --output-dir Dados/Migrations; fi
mostra dotnet ef migrations list --project $WEB
etapa "Script SQL gerado para revisão"
dotnet ef migrations script --project $WEB --idempotent -o "$EV/03_migration_inicial.sql" 2>&1 | tail -2
echo "(script completo em evidencias/03_migration_inicial.sql)"
etapa "Aplicando ao banco"
mostra dotnet ef database update --project $WEB
} > "$EV/03_migrations.txt"
dotnet build PortalTarefas.slnx -c Debug > /dev/null 2>&1

# ---------------------------------------------------------------- API em execução (1ª vez: seed)
dotnet dev-certs https --trust >/dev/null 2>&1
# No Linux o certificado confiável fica nesta pasta; o HttpClient da API passa a aceitá-lo (como no Windows/Visual Studio)
export SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs"
iniciar_api /tmp/api_1.log
{
etapa "Banco após a primeira execução (seed aplicado)"
echo "\$ sqlcmd -Q \"SELECT name FROM sys.tables ORDER BY name\""; sql "SET NOCOUNT ON; SELECT name FROM sys.tables ORDER BY name"
echo "\$ sqlcmd -Q \"SELECT COUNT(*) FROM Tarefas\""; sql "SET NOCOUNT ON; SELECT COUNT(*) AS Tarefas FROM Tarefas"
} > "$EV/03_seed.txt"
parar_api
iniciar_api /tmp/api_2.log
{
etapa "Depois de reiniciar a aplicação (seed idempotente: nada duplicado)"
echo "\$ sqlcmd -Q \"SELECT COUNT(*) FROM Tarefas\""; sql "SET NOCOUNT ON; SELECT COUNT(*) AS Tarefas FROM Tarefas"
echo "\$ sqlcmd -Q \"SELECT Id, Titulo, Situacao, Equipe, CONVERT(varchar(18), RowVersion, 1) AS RowVersion FROM Tarefas\""
sql "SET NOCOUNT ON; SELECT Id, LEFT(Titulo,32) AS Titulo, Situacao, Equipe, CONVERT(varchar(18), RowVersion, 1) AS RowVersion FROM Tarefas"
} >> "$EV/03_seed.txt"

# ---------------------------------------------------------------- telas MVC (Unidades 3, 4 e 6)
(cd ci-evidencias && npm install --silent >/dev/null 2>&1 && npx playwright install --with-deps chromium >/dev/null 2>&1)
(cd ci-evidencias && node telas.mjs mvc) > "$EV/telas_mvc.log" 2>&1
{
etapa "Auditoria gravada na mesma transação"
echo "\$ sqlcmd -Q \"SELECT * FROM EventosAuditoria\""; sql "SET NOCOUNT ON; SELECT Id, TarefaId, Acao, Detalhe, OcorridoEmUtc FROM EventosAuditoria"
etapa "Tarefa 4 depois do conflito (a alteração da aba A permanece; a da aba B não sobrescreveu)"
sql "SET NOCOUNT ON; SELECT Id, Titulo, Situacao, CONVERT(varchar(18), RowVersion, 1) AS RowVersion FROM Tarefas WHERE Id = 4"
} > "$EV/03_transacao_concorrencia.txt"
grep -E "Conflito de concorrência|Commit|Rolling back|ROLLBACK" /tmp/api_2.log | head -5 >> "$EV/03_transacao_concorrencia.txt"

# ---------------------------------------------------------------- Unidade 3/4/6/7: requisições
for u in admin ana bruno carla; do entrar $u > /dev/null; done
req 03_painel_ef_dapper -b /tmp/ck_admin "$B/api/painel?fonte=ef"
req 03_painel_ef_dapper -b /tmp/ck_admin "$B/api/painel?fonte=dapper"

R=04_api_contrato
req $R -b /tmp/ck_admin "$B/api/v1/tarefas?pagina=1&tamanho=3&ordenarPor=prazo&direcao=desc"
req $R -b /tmp/ck_admin "$B/api/v1/tarefas?tamanho=0"
req $R -b /tmp/ck_admin "$B/api/v1/tarefas?tamanho=51"
req $R -b /tmp/ck_admin "$B/api/v1/tarefas?ordenarPor=descricao"
req $R -b /tmp/ck_admin "$B/api/v1/tarefas?situacao=EmAndamento&prioridade=Alta"
req $R -b /tmp/ck_admin "$B/api/v1/tarefas?pagina=99"
req $R -b /tmp/ck_admin "$B/api/v1/tarefas/9999"
req $R -b /tmp/ck_admin -X POST "$B/api/v1/tarefas" -H "Content-Type: application/json" -d '{"titulo":"Gerar cliente TypeScript","prioridade":"Media","prazo":"2026-10-30"}'
req $R -b /tmp/ck_admin -X POST "$B/api/v1/tarefas" -H "Content-Type: application/json" -d '{"titulo":"","prioridade":"Alta"}'
V=$(curl -sk -b /tmp/ck_admin $B/api/v1/tarefas/5 | python3 -c "import json,sys;print(json.load(sys.stdin)['versao'])")
req 04_api_conflito -b /tmp/ck_admin -X PUT "$B/api/v1/tarefas/5" -H "Content-Type: application/json" -d "{\"titulo\":\"Persistir tarefas com EF Core\",\"prioridade\":\"Alta\",\"situacao\":\"Concluida\",\"versao\":\"$V\"}"
req 04_api_conflito -b /tmp/ck_admin -X PUT "$B/api/v1/tarefas/5" -H "Content-Type: application/json" -d "{\"titulo\":\"Persistir tarefas com EF Core\",\"prioridade\":\"Alta\",\"situacao\":\"Pendente\",\"versao\":\"$V\"}"
{ echo "\$ curl -s $B/openapi/v1.json | head -60"; curl -sk $B/openapi/v1.json | python3 -m json.tool | head -60; } > "$EV/04_openapi.txt"
{
etapa "Geração do cliente TypeScript com NSwag"
cd clients/typescript
mostra dotnet nswag run nswag.json
ls -la src
mostra npm install --silent
etapa "Chamada real com o cliente gerado"
echo "\$ npm run chamada-real"
NODE_TLS_REJECT_UNAUTHORIZED=0 PORTAL_EMAIL=admin@portal.local PORTAL_SENHA="$SEED_PWD" npx tsx src/chamada-real.ts 2>&1 | grep -v NODE_TLS
cd "$RAIZ"
} > "$EV/04_nswag.txt" 2>&1

sleep 31   # nova janela do limite de escrita
R=06_matriz
{ echo "Matriz de autorização obtida chamando a API diretamente (fora da SPA)"; echo
  printf "%-36s %-8s %-8s %-8s %-8s %-8s\n" "Operação" anonimo bruno ana carla admin
  for op in "GET /api/v1/tarefas" "POST /api/v1/tarefas" "PUT /api/v1/tarefas/1" "DELETE /api/v1/tarefas/2"; do
    linha=$(printf "%-36s" "$op")
    for u in anonimo bruno ana carla admin; do
      ck=""; [ $u != anonimo ] && ck="-b /tmp/ck_$u"
      m=${op%% *}; url=$B${op#* }
      if [ $m = GET ]; then c=$(curl -sk $ck -o /dev/null -w "%{http_code}" $url)
      elif [ $m = POST ]; then c=$(curl -sk $ck -o /dev/null -w "%{http_code}" -X POST -H 'Content-Type: application/json' -d '{"titulo":"Matriz de testes","prioridade":"Baixa"}' $url)
      elif [ $m = PUT ]; then v=$(curl -sk -b /tmp/ck_admin $B/api/v1/tarefas/1 | python3 -c "import json,sys;print(json.load(sys.stdin)['versao'])")
        c=$(curl -sk $ck -o /dev/null -w "%{http_code}" -X PUT -H 'Content-Type: application/json' -d "{\"titulo\":\"Configurar pipeline HTTP\",\"prioridade\":\"Alta\",\"situacao\":\"Concluida\",\"versao\":\"$v\"}" $url)
      else [ $u = admin ] && url=$B/api/v1/tarefas/6; c=$(curl -sk $ck -o /dev/null -w "%{http_code}" -X DELETE $url); fi
      linha="$linha $(printf '%-8s' $c)"
    done; echo "$linha"
  done
  echo; echo "(DELETE do admin feito na tarefa 6, para não apagar a tarefa usada pelos demais)"
} > "$EV/$R.txt"
req 06_login -X POST $B/api/auth/login -H "Content-Type: application/json" -d '{"email":"ana@portal.local","senha":"errada"}'
req 06_login -X POST $B/api/auth/login -H "Content-Type: application/json" -d '{"email":"ninguem@portal.local","senha":"errada"}'
req 06_login -b /tmp/ck_ana $B/api/auth/me
curl -sk -b /tmp/ck_ana -c /tmp/ck_ana -X POST $B/api/auth/logout -o /dev/null
req 06_login -b /tmp/ck_ana $B/api/auth/me

# ---------------------------------------------------------------- Unidade 5/6: SPA
(cd src/PortalTarefas.Frontend && npm ci --silent >/dev/null 2>&1 && (nohup npx vite --port 5173 > /tmp/vite.log 2>&1 &) )
for i in $(seq 1 30); do curl -s -o /dev/null http://localhost:5173/app/ && break; sleep 1; done
(cd ci-evidencias && node telas.mjs spa) > "$EV/telas_spa.log" 2>&1

# ---------------------------------------------------------------- Unidade 7
R=07_saude
req $R $B/health/live
req $R $B/health/ready
curl -sk -X POST $B/simulador/dicas/modo/fora -o /dev/null
req $R $B/health/live
req $R $B/health/ready
curl -sk -X POST $B/simulador/dicas/modo/normal -o /dev/null
entrar admin > /dev/null
{
etapa "Falha transitória: 2 de cada 3 chamadas ao serviço de dicas falham; o retry resolve"
curl -sk -X POST $B/simulador/dicas/modo/transitorio -o /dev/null
echo "\$ curl $B/api/v1/dica"; curl -sk -b /tmp/ck_admin $B/api/v1/dica; echo
etapa "Serviço fora do ar: falhas sucessivas abrem o circuito e o fallback responde"
curl -sk -X POST $B/simulador/dicas/modo/fora -o /dev/null
for i in 1 2 3 4 5 6 7 8; do printf "chamada %s: " $i; curl -sk -b /tmp/ck_admin -o /tmp/d.json -w "%{http_code} %{time_total}s " $B/api/v1/dica; cat /tmp/d.json; echo; done
curl -sk -X POST $B/simulador/dicas/modo/normal -o /dev/null
} > "$EV/07_resiliencia.txt"
grep -E "Resilience event|Polly|circuit|Circuit|LogicalHandler.*(Start|End) processing|ServicoDicas|fallback" /tmp/api_2.log | sed -E 's/\x1b\[[0-9;]*m//g' | head -40 > "$EV/07_resiliencia_log.txt"
sleep 31   # janela nova do rate limit
{
etapa "Rate limiting: 6 escritas seguidas pelo mesmo usuário (limite 5 a cada 30 s)"
for i in 1 2 3 4 5 6; do printf "POST %s -> " $i; curl -sk -b /tmp/ck_admin -o /tmp/r.json -D /tmp/h.txt -w "%{http_code}" -X POST -H 'Content-Type: application/json' -d "{\"titulo\":\"Carga controlada $i\",\"prioridade\":\"Baixa\"}" $B/api/v1/tarefas; grep -i "retry-after" /tmp/h.txt | tr -d '\r' | sed 's/^/  /'; echo; done
echo; echo "Corpo da resposta 429:"; python3 -m json.tool --no-ensure-ascii /tmp/r.json
} > "$EV/07_rate_limit.txt"
R=07_cache
req $R $B/api/publico/estatisticas
sleep 2
req $R $B/api/publico/estatisticas
sleep 31; curl -sk -b /tmp/ck_admin -o /dev/null -X POST -H 'Content-Type: application/json' -d '{"titulo":"Nova tarefa invalida o cache","prioridade":"Baixa"}' $B/api/v1/tarefas
req $R $B/api/publico/estatisticas
req 07_erro_correlacionado $B/api/diagnostico/erro
ID=$(grep -i "x-correlation-id" "$EV/07_erro_correlacionado.txt" | head -1 | awk '{print $2}')
{ echo "\$ grep $ID (log da aplicação)"; grep -F "$ID" /tmp/api_2.log | sed -E 's/\x1b\[[0-9;]*m//g' | cut -c1-260; } >> "$EV/07_erro_correlacionado.txt"
sleep 32   # aguarda a exportação periódica das métricas
grep -n -A14 "Activity.DisplayName: *Tarefas.Criar" /tmp/api_2.log | head -40 > "$EV/07_trace.txt"
grep -n -B2 -A12 "portal.tarefas.criadas" /tmp/api_2.log | head -40 > "$EV/07_metricas.txt"

# ---------------------------------------------------------------- testes automatizados (Unidades 1 a 7)
sleep 31
{ echo "\$ cd tests/PortalTarefas.Tests && dotnet run"
  (cd tests/PortalTarefas.Tests && PORTAL_SENHA_TESTE="$SEED_PWD" dotnet run --no-build 2>&1); } > "$EV/testes_integracao.txt"
{ echo "\$ cd src/PortalTarefas.Frontend && npm test"; (cd src/PortalTarefas.Frontend && npx vitest run 2>&1 | sed -E 's/\x1b\[[0-9;]*m//g'); } > "$EV/testes_frontend.txt"

cp /tmp/api_2.log "$EV/log_aplicacao.txt"
sed -i -E 's/\x1b\[[0-9;]*m//g' "$EV/log_aplicacao.txt"
parar_api

# ---------------------------------------------------------------- Unidade 8: imagem e contêiner
{
etapa "Build da imagem (multi-stage)"
docker build -t portal-tarefas:ci . 2>&1 | grep -E "^#[0-9]+ \[|DONE|naming|exporting|ERROR" | tail -40
etapa "Usuário e variáveis da imagem (nenhum segredo)"
mostra docker image inspect portal-tarefas:ci --format 'User={{.Config.User}}  Env={{.Config.Env}}'
mostra docker image ls portal-tarefas:ci
etapa "Execução com configuração externa"
echo "\$ docker run -d --name api --network rede -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Staging -e ConnectionStrings__PortalTarefas=*** portal-tarefas:ci"
docker run -d --name api --network rede -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Staging \
  -e "ConnectionStrings__PortalTarefas=Server=sql;Database=PortalTarefas;User Id=sa;Password=$SQL_PWD;TrustServerCertificate=True" portal-tarefas:ci | cut -c1-12
for i in $(seq 1 40); do s=$(docker inspect -f '{{.State.Health.Status}}' api); [ "$s" = healthy ] && break; sleep 3; done
mostra docker ps --filter name=api --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}'
echo "\$ curl -i http://localhost:8080/health/live"; curl -s -i http://localhost:8080/health/live | tr -d '\r' | grep -viE "^(date|server):"; echo
echo "\$ curl http://localhost:8080/health/ready"; curl -s http://localhost:8080/health/ready; echo
etapa "Segredo não está nas camadas da imagem"
echo "\$ docker history --no-trunc portal-tarefas:ci | grep -c Password"; docker history --no-trunc portal-tarefas:ci | grep -c Password
etapa "Primeiras linhas do log do contêiner"
docker logs api 2>&1 | head -12 | cut -c1-200
} > "$EV/08_container.txt" 2>&1
(cd ci-evidencias && node telas.mjs container) > "$EV/telas_container.log" 2>&1
echo "fim"
