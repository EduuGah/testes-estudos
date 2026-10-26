using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Endpoints;
using PortalTarefas.Web.Filtros;
using PortalTarefas.Web.Middleware;
using PortalTarefas.Web.Servicos;

var builder = WebApplication.CreateBuilder(args);

// Logs em uma linha e com horário (o CorrelationId vai na própria mensagem)
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.IncludeScopes = false;
    o.TimestampFormat = "HH:mm:ss ";
});

// ---------- Serviços ----------
builder.Services.AddProblemDetails();                 // erros no formato Problem Details
builder.Services.AddControllersWithViews();             // Unidade 2: controllers + views Razor
builder.Services.AddScoped<RegistroAcaoFilter>();       // filtro resolvido por injeção ([ServiceFilter])

// Unidade 3: EF Core + SQL Server. A connection string vem de User Secrets em
// desenvolvimento e de variável de ambiente em produção — nunca do repositório.
var conexao = builder.Configuration.GetConnectionString("PortalTarefas");
if (string.IsNullOrWhiteSpace(conexao))
    throw new InvalidOperationException("ConnectionStrings:PortalTarefas não configurada (use dotnet user-secrets).");
builder.Services.AddDbContextPool<PortalTarefasDbContext>(opcoes => opcoes.UseSqlServer(conexao));
builder.Services.AddScoped<ITarefaServico, TarefaServicoEf>();
builder.Services.AddScoped<PainelConsultas>();
builder.Services.AddCors(opcoes =>
{
    // Política nomeada: somente o front-end local (Vite) pode chamar a API pelo navegador.
    opcoes.AddPolicy("FrontendLocal", politica => politica
        .WithOrigins("http://localhost:5173")
        .WithMethods("GET", "POST", "PUT", "DELETE")
        .WithHeaders("Content-Type", "X-Cliente-Id", "X-Correlation-Id"));
});

var app = builder.Build();

// Em desenvolvimento, aplica migrations pendentes e o seed idempotente na inicialização.
if (app.Environment.IsDevelopment())
{
    using var escopo = app.Services.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<PortalTarefasDbContext>();
    await db.Database.MigrateAsync();
    await SeedDados.AplicarAsync(db);
}

// ---------- Pipeline (a ordem importa) ----------
app.UseExceptionHandler();        // 0. captura exceções de tudo o que vem depois
app.UseCorrelacao();              // 1. extensão: X-Correlation-Id + escopo de log
app.UseRegistroRequisicao();      // 2. middleware próprio: método, caminho, status e duração
app.UseHttpsRedirection();        // 3. redireciona HTTP -> HTTPS
app.UseStaticFiles();             //    arquivos de wwwroot encerram a requisição aqui (curto-circuito)
app.UseRouting();                 //    escolhe o endpoint
app.UseCors("FrontendLocal");     //    aplica CORS depois do roteamento e antes dos endpoints

// ---------- Endpoints ----------
app.MapTarefasEndpoints();

// Diagnóstico em Minimal API (equivalente ao controller MVC)
app.MapGet("/api/diagnostico", async (ITarefaServico servico, IWebHostEnvironment ambiente) => Results.Ok(new
{
    abordagem = "Minimal API",
    ambiente = ambiente.EnvironmentName,
    totalTarefas = await servico.ContarAsync(),
    horarioUtc = DateTime.UtcNow
}));

// Endpoint que lança exceção de propósito, para testar o tratamento centralizado
app.MapGet("/api/diagnostico/erro", string () =>
    throw new InvalidOperationException("Falha simulada no repositório (detalhe interno que não deve chegar ao cliente)."));

// Painel: mesma leitura com EF Core e com Dapper, para comparação
app.MapGet("/api/painel", async (string? fonte, PainelConsultas painel, CancellationToken ct) =>
{
    var hoje = DateOnly.FromDateTime(DateTime.Today);
    var linhas = fonte == "dapper" ? await painel.ComDapperAsync(hoje, ct) : await painel.ComEfCoreAsync(hoje, ct);
    return Results.Ok(new { fonte = fonte ?? "ef", linhas });
});

app.MapControllers();                                   // controllers com [Route] (API de diagnóstico)
app.MapControllerRoute(                                  // rota convencional da interface MVC
    name: "default",
    pattern: "{controller=Tarefas}/{action=Index}/{id?}");

app.Run();

public partial class Program;   // permite referenciar a aplicação em testes de integração
