# Unidade 3 – Comandos de banco de dados

Ferramentas: SQL Server LocalDB (Visual Studio 2026) e EF Core 10.

## 1. Segredo de conexão (fora do repositório)
```powershell
cd src/PortalTarefas.Web
dotnet user-secrets set "ConnectionStrings:PortalTarefas" "Server=(localdb)\MSSQLLocalDB;Database=PortalTarefas;Trusted_Connection=True;TrustServerCertificate=True"
```

## 2. Migrations
Console do Gerenciador de Pacotes (Visual Studio):
```powershell
Add-Migration Inicial
Script-Migration            # gera o SQL para revisão antes de aplicar
Update-Database
```
Ou pela CLI:
```bash
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet ef migrations add Inicial --project src/PortalTarefas.Web
dotnet ef migrations script --project src/PortalTarefas.Web -o docs/inicial.sql
dotnet ef database update --project src/PortalTarefas.Web
```
Extensão (índice por migration): o índice composto `IX_Tarefas_Situacao_Prazo`, usado
pelo painel e pelos filtros, é declarado em `TarefaConfiguration` e aparece no SQL da migration.

## 3. Instalação limpa
```bash
dotnet ef database drop --force --project src/PortalTarefas.Web
dotnet run --project src/PortalTarefas.Web   # aplica migrations e seed na inicialização
```

## 4. Reproduzir concorrência (duas sessões)
1. Abrir /Tarefas/Edit/3 em duas abas (A e B).
2. Na aba A, mudar o título e salvar → sucesso, rowversion muda no banco.
3. Na aba B (ainda com o rowversion antigo), mudar a situação e salvar →
   DbUpdateConcurrencyException tratada; a tela mostra os valores atuais e nada é sobrescrito.

## 5. Conferência no banco
```sql
SELECT Id, Titulo, Situacao, CONVERT(varchar(20), RowVersion, 1) AS RowVersion FROM Tarefas;
SELECT * FROM EventosAuditoria ORDER BY Id DESC;
```
