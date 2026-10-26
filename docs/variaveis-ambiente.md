# Variáveis de configuração (sem valores secretos)

| Chave (appsettings) | Variável de ambiente | Obrigatória | Sensível | Origem por ambiente |
|---|---|---|---|---|
| ConnectionStrings:PortalTarefas | ConnectionStrings__PortalTarefas | Sim | **Sim** | Dev: User Secrets · Staging/Prod: secret store do provedor |
| SeedIdentidade:Senha | SeedIdentidade__Senha | Só Dev/CI | **Sim** | Dev: User Secrets · CI: gerada no job |
| ServicoDicas:UrlBase | ServicoDicas__UrlBase | Sim | Não | appsettings.{Ambiente}.json ou variável |
| Portal:NomeAmbiente | Portal__NomeAmbiente | Sim | Não | appsettings.{Ambiente}.json |
| Portal:OrigensPermitidas | Portal__OrigensPermitidas__0 | Sim | Não | appsettings.{Ambiente}.json |
| ASPNETCORE_ENVIRONMENT | — | Sim | Não | Development / Staging / Production |

Precedência (a última vence): appsettings.json → appsettings.{Ambiente}.json → User Secrets (só Development)
→ variáveis de ambiente → argumentos de linha de comando.
