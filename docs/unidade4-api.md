# Unidade 4 – Contrato HTTP e cliente TypeScript

| Operação | Método e URI | Sucesso | Erros |
|---|---|---|---|
| Listar | GET /api/v1/tarefas?pagina&tamanho&ordenarPor&direcao&situacao&prioridade&busca | 200 PaginaDto | 400 |
| Detalhar | GET /api/v1/tarefas/{id} | 200 TarefaDetalheDto | 404 |
| Criar | POST /api/v1/tarefas | 201 + Location | 400 |
| Atualizar | PUT /api/v1/tarefas/{id} | 200 | 400, 404, 409 |
| Excluir | DELETE /api/v1/tarefas/{id} | 204 | 404 |

Regras: `pagina` ≥ 1 (padrão 1); `tamanho` 1..50 (padrão 10); `ordenarPor` ∈ {id, titulo, prazo, prioridade, situacao};
`direcao` ∈ {asc, desc}. Erros sempre em Problem Details (RFC 9457).

## Documento OpenAPI
* Em execução (Development): `GET /openapi/v1.json`; interface Scalar em `/scalar/v1`.
* No build: `src/PortalTarefas.Web/openapi/PortalTarefas.Web.json` (pacote Microsoft.Extensions.ApiDescription.Server),
  versionado no Git para que mudanças de contrato apareçam no diff do pull request.

## Regenerar o cliente quando o contrato mudar
```bash
dotnet build src/PortalTarefas.Web          # atualiza openapi/PortalTarefas.Web.json
cd clients/typescript && npm run gerar       # NSwag lê o JSON e reescreve src/portal-tarefas-client.ts
npm run chamada-real                         # chamada real com o cliente gerado
```
Mudança compatível usada no teste: inclusão do filtro opcional `busca` → o método `listarTarefas` ganhou um parâmetro opcional
e o código existente continuou compilando.
