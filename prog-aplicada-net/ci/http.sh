#!/usr/bin/env bash
# Evidências HTTP dos roteiros 6 (versões) e 7 (ETag e output cache). A API precisa estar em execução.
U=http://localhost:5080
H='^(HTTP|etag|api-|deprecation|sunset|age|x-gerado-em|cache-control|content-length|location)'
mostrar() { echo; echo "\$ $1"; }
cab() { grep -iE "$H" | tr -d '\r'; }

ID=$(curl -s -X POST $U/api/v1/pedidos -H 'X-Usuario: ana' -H 'Content-Type: application/json' \
  -d '{"regiao":"Sudeste","prioridade":"Normal","itens":[{"sku":"CAFE-01","quantidade":2,"precoUnitario":19.90},{"sku":"XICARA","quantidade":1,"precoUnitario":35}],"canal":"app"}' \
  | python3 -c 'import sys,json; print(json.load(sys.stdin)["id"])')
sleep 1
echo "pedido criado: $ID"

echo; echo "################ ROTEIRO 6 – versões"
mostrar "GET /api/v1/pedidos/{id}   (contrato v1)"
curl -s -D - -H 'X-Usuario: ana' $U/api/v1/pedidos/$ID -o /tmp/v1.json | cab; python3 -m json.tool --compact /tmp/v1.json
mostrar "GET /api/v2/pedidos/{id}   (contrato v2)"
curl -s -D - -H 'X-Usuario: ana' $U/api/v2/pedidos/$ID -o /tmp/v2.json | cab; python3 -m json.tool --compact /tmp/v2.json
mostrar "GET /api/pedidos/{id}   (sem versão -> padrão v1)"
curl -s -D - -H 'X-Usuario: ana' $U/api/pedidos/$ID -o /dev/null | cab
mostrar "GET /api/v9/pedidos/{id}   (versão não suportada)"
curl -s -H 'X-Usuario: ana' $U/api/v9/pedidos/$ID -w '\nHTTP %{http_code}\n'

echo; echo "################ ROTEIRO 7 – ETag"
mostrar "GET /api/v1/pedidos/{id}"
ETAG=$(curl -s -D - -H 'X-Usuario: ana' $U/api/v1/pedidos/$ID -o /dev/null | tee /tmp/h1 | grep -i '^etag' | cut -d' ' -f2 | tr -d '\r'); cab < /tmp/h1
mostrar "GET com If-None-Match: $ETAG"
curl -s -D - -H 'X-Usuario: ana' -H "If-None-Match: $ETAG" $U/api/v1/pedidos/$ID -o /tmp/corpo304 | cab; echo "bytes no corpo: $(wc -c < /tmp/corpo304)"
mostrar "PUT /status sem If-Match"
curl -s -X PUT -H 'X-Usuario: ana' -H 'Content-Type: application/json' -d '{"status":"Cancelado"}' $U/api/v1/pedidos/$ID/status -w '\nHTTP %{http_code}\n' | tail -1
mostrar "PUT /status com If-Match obsoleto"
curl -s -X PUT -H 'X-Usuario: ana' -H 'If-Match: "0000"' -H 'Content-Type: application/json' -d '{"status":"Cancelado"}' $U/api/v1/pedidos/$ID/status -w '\nHTTP %{http_code}\n' | tail -1
mostrar "PUT /status com If-Match: $ETAG"
curl -s -D - -X PUT -H 'X-Usuario: ana' -H "If-Match: $ETAG" -H 'Content-Type: application/json' -d '{"status":"Cancelado"}' $U/api/v1/pedidos/$ID/status -o /dev/null | cab
mostrar "GET com If-None-Match antigo: $ETAG"
curl -s -D - -H 'X-Usuario: ana' -H "If-None-Match: $ETAG" $U/api/v1/pedidos/$ID -o /dev/null | cab

echo; echo "################ ROTEIRO 7 – output cache (rota pública /api/v1/estatisticas)"
for i in 1 2 3; do mostrar "GET anônimo #$i"; curl -s -D - $U/api/v1/estatisticas -o /dev/null | cab; sleep 0.5; done
mostrar "GET autenticado (não usa o cache compartilhado)"
curl -s -D - -H 'X-Usuario: ana' $U/api/v1/estatisticas -o /dev/null | cab
mostrar "POST novo pedido (invalida a tag 'pedidos') e GET anônimo"
curl -s -o /dev/null -X POST $U/api/v1/pedidos -H 'X-Usuario: ana' -H 'Content-Type: application/json' -d '{"regiao":"Sul","itens":[{"sku":"A","quantidade":1,"precoUnitario":10}]}'
sleep 0.5; curl -s -D - $U/api/v1/estatisticas -o /dev/null | cab

mostrar "Latência: 20 GET anônimos com cache"
for i in $(seq 20); do curl -s -o /dev/null -w '%{time_total}\n' $U/api/v1/estatisticas; done | python3 -c 'import sys; v=sorted(float(x)*1000 for x in sys.stdin); print(f"mediana {v[len(v)//2]:.2f} ms, máx {v[-1]:.2f} ms")'
mostrar "Latência: 20 GET autenticados (sem cache, consulta o banco)"
for i in $(seq 20); do curl -s -o /dev/null -H 'X-Usuario: ana' -w '%{time_total}\n' $U/api/v1/estatisticas; done | python3 -c 'import sys; v=sorted(float(x)*1000 for x in sys.stdin); print(f"mediana {v[len(v)//2]:.2f} ms, máx {v[-1]:.2f} ms")'
