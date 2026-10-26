# Unidade 8 – Build, contêiner e CI/CD

## Comandos locais
```bash
cp .env.example .env              # preencher SQL_SA_PASSWORD localmente
docker compose up --build -d
docker compose ps                 # coluna STATUS mostra (healthy) quando o HEALTHCHECK passa
curl -s http://localhost:8080/health/live
docker image inspect portal-tarefas:local --format '{{.Config.User}}'   # usuário não-root
docker history portal-tarefas:local    # nenhuma camada contém connection string
```

## Integrado x separado
| Critério | Integrado (escolhido) | Separado |
|---|---|---|
| Hosts/domínios | 1 | 2 (API + site estático/CDN) |
| CORS e cookie | mesma origem: sem CORS em produção e cookie SameSite=Strict funciona | exige CORS com credenciais e SameSite=None |
| Publicação | um artefato por commit | ciclos independentes |
| Rollback | uma imagem anterior | dois artefatos a coordenar |
| Escala | conjunta | independente |

Decisão: **integrado**. O projeto é pequeno, mantido por uma pessoa, e a autenticação por cookie (Unidade 6)
depende da mesma origem.

## Promoção e rollback
* A imagem é construída **uma vez** por commit (`:<sha>`) e promovida sem rebuild: staging → produção.
* Rollback: reimplantar a tag do commit anterior aprovado (`azure webapp config container set ... :<sha-anterior>`);
  a migration do banco só é aplicada por bundle (`dotnet ef migrations bundle`) e sempre de forma compatível com a versão anterior.
* Falha deliberada: alterar uma asserção em `Unidade1PipelineTestes` faz o job `backend` falhar → `publicar` não executa.
