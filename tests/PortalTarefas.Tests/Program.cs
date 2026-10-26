using PortalTarefas.Tests;

// Uso: dotnet run -- [filtro]   (variável PORTAL_API_URL aponta para a API em execução)
return await ExecutorTestes.ExecutarAsync(typeof(ExecutorTestes).Assembly, args.FirstOrDefault());
