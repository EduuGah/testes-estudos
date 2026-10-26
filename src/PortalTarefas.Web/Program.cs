using PortalTarefas.Web.Endpoints;
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
builder.Services.AddControllers();                     // necessário para o controller MVC de comparação
builder.Services.AddSingleton<RepositorioTarefasMemoria>();
builder.Services.AddCors(opcoes =>
{
    // Política nomeada: somente o front-end local (Vite) pode chamar a API pelo navegador.
    opcoes.AddPolicy("FrontendLocal", politica => politica
        .WithOrigins("http://localhost:5173")
        .WithMethods("GET", "POST", "PUT", "DELETE")
        .WithHeaders("Content-Type", "X-Cliente-Id", "X-Correlation-Id"));
});

var app = builder.Build();

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
app.MapGet("/api/diagnostico", (RepositorioTarefasMemoria repositorio, IWebHostEnvironment ambiente) => Results.Ok(new
{
    abordagem = "Minimal API",
    ambiente = ambiente.EnvironmentName,
    totalTarefas = repositorio.Total,
    horarioUtc = DateTime.UtcNow
}));

// Endpoint que lança exceção de propósito, para testar o tratamento centralizado
app.MapGet("/api/diagnostico/erro", string () =>
    throw new InvalidOperationException("Falha simulada no repositório (detalhe interno que não deve chegar ao cliente)."));

app.MapControllers();

app.Run();

public partial class Program;   // permite referenciar a aplicação em testes de integração
