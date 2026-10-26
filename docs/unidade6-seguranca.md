# Unidade 6 – Autenticação e autorização

## Requisitos considerados para escolher o esquema
* Clientes: páginas MVC e SPA React, ambas em navegador.
* Origem: em desenvolvimento a SPA usa o proxy do Vite; em produção o React é servido pela própria API
  (deploy integrado da Unidade 8) → **mesma origem**.
* Não há aplicativo móvel nem integração entre sistemas nesta versão.

**Decisão: cookie do ASP.NET Core Identity** (`HttpOnly`, `Secure`, `SameSite=Strict`, 30 min deslizantes).
* Token bearer do Identity (`MapIdentityApi`) ou JWT de um provedor OAuth/OIDC ficariam para clientes não-navegador.
  Exigiriam guardar o token em memória/armazenamento acessível ao JavaScript e implementar renovação/revogação.
* Riscos do cookie e controles: CSRF → `SameSite=Strict` + antiforgery nos formulários MVC + API só aceita JSON;
  roubo por XSS → `HttpOnly`; tráfego em claro → `Secure` + HTTPS/HSTS; sessão longa → expiração de 30 min e logout.

## Configuração da senha de seed (fora do repositório)
```powershell
dotnet user-secrets set "SeedIdentidade:Senha" "<senha forte escolhida localmente>"
Add-Migration Identidade
Update-Database
```

## Usuários de teste (criados por seed controlado)
| Usuário | Role | Claims |
|---|---|---|
| ana@portal.local | Usuario | equipe=Web |
| bruno@portal.local | Usuario | equipe=Dados |
| carla@portal.local | Usuario | equipe=Coordenacao, permissao=editar-outras-equipes |
| admin@portal.local | Administrador | equipe=Geral |

## Matriz de autorização esperada
| Operação | Anônimo | Usuário (outra equipe) | Usuário (mesma equipe / dono) | Carla (claim) | Administrador |
|---|---|---|---|---|---|
| GET /api/v1/tarefas | 401 | 200 | 200 | 200 | 200 |
| POST /api/v1/tarefas | 401 | 201 | 201 | 201 | 201 |
| PUT /api/v1/tarefas/{id} | 401 | **403** | 200 | 200 | 200 |
| DELETE /api/v1/tarefas/{id} | 401 | 403 | 403 | 403 | 204 |

Os testes chamam a API diretamente (fora da SPA) para provar que o servidor é a autoridade final.
