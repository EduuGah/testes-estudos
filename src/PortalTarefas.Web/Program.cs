using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Endpoints;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Filtros;
using PortalTarefas.Web.Integracoes;
using PortalTarefas.Web.Observabilidade;
using PortalTarefas.Web.Seguranca;
using PortalTarefas.Web.Middleware;
using PortalTarefas.Web.Servicos;

var builder = WebApplication.CreateBuilder(args);

// Unidade 7: logs estruturados, OpenTelemetry, resiliência, health checks, rate limiting e cache
builder.AdicionarObservabilidadeEResiliencia();

// ---------- Serviços ----------
builder.Services.AddProblemDetails(o =>                // erros no formato Problem Details
    // Unidade 7: todo erro carrega o id de correlação, que localiza os logs da falha
    o.CustomizeProblemDetails = ctx => ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.TraceIdentifier);
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

// Unidade 6: Identity + cookie de autenticação
builder.Services
    .AddIdentity<UsuarioApp, IdentityRole>(o =>
    {
        // Requisitos didáticos, porém sem desligar controles: senha forte e bloqueio após 5 erros
        o.Password.RequiredLength = 10;
        o.Password.RequireNonAlphanumeric = true;
        o.Password.RequireUppercase = true;
        o.Password.RequireDigit = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<PortalTarefasDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "PortalTarefas.Auth";
    o.Cookie.HttpOnly = true;                              // JavaScript não lê o cookie
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;     // só trafega em HTTPS
    o.Cookie.SameSite = SameSiteMode.Strict;               // não é enviado em requisições de outros sites (CSRF)
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    o.SlidingExpiration = true;
    o.LoginPath = "/Conta/Entrar";
    o.AccessDeniedPath = "/Conta/AcessoNegado";
    // Chamadas de API recebem 401/403 em vez de redirecionamento para uma página HTML
    o.Events.OnRedirectToLogin = ctx => ResponderOuRedirecionar(ctx, StatusCodes.Status401Unauthorized);
    o.Events.OnRedirectToAccessDenied = ctx => ResponderOuRedirecionar(ctx, StatusCodes.Status403Forbidden);

    static Task ResponderOuRedirecionar(Microsoft.AspNetCore.Authentication.RedirectContext<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> ctx, int status)
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = status;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    }
});
builder.Services.AddAuthorization(Politicas.Registrar);
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, EditarTarefaHandler>();

// Unidade 4: API REST documentada
builder.Services.AddScoped<TarefasApiServico>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi("v1", opcoes =>
{
    opcoes.AddDocumentTransformer((documento, _, _) =>
    {
        documento.Info.Title = "Portal de Tarefas API";
        documento.Info.Version = "v1";
        documento.Info.Description = "API do domínio didático Portal de Tarefas (UNIP – Desenvolvimento Web com .NET).";
        return Task.CompletedTask;
    });
});
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
    await SeedIdentidade.AplicarAsync(escopo.ServiceProvider, app.Configuration, app.Logger);
}

// ---------- Pipeline (a ordem importa) ----------
app.UseExceptionHandler();        // 0. captura exceções de tudo o que vem depois
app.UseCorrelacao();              // 1. extensão: X-Correlation-Id + escopo de log
app.UseRegistroRequisicao();      // 2. middleware próprio: método, caminho, status e duração
app.UseHttpsRedirection();        // 3. redireciona HTTP -> HTTPS
app.UseStaticFiles();             //    arquivos de wwwroot encerram a requisição aqui (curto-circuito)
app.UseRouting();                 //    escolhe o endpoint
app.UseCors("FrontendLocal");     //    aplica CORS depois do roteamento e antes dos endpoints
app.UseAuthentication();          //    Unidade 6: lê o cookie e preenche HttpContext.User
app.UseAuthorization();           //    depois aplica [Authorize] e políticas
app.UseRateLimiter();             //    Unidade 7: limites por política (particionados por usuário/IP)
app.UseOutputCache();             //    Unidade 7: cache de respostas públicas

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

// Endpoint que lança exceção de propósito, para testar o tratamento centralizado (somente Development)
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/diagnostico/erro", string () =>
        throw new InvalidOperationException("Falha simulada no repositório (detalhe interno que não deve chegar ao cliente)."));
}

app.MapTarefasApiV1();
app.MapAutenticacaoEndpoints();
app.MapSaude();                   // /health/live e /health/ready

// Leitura pública e não personalizada → candidata a output caching
app.MapGet("/api/publico/estatisticas", async (PortalTarefasDbContext db, CancellationToken ct) =>
    Results.Ok(new
    {
        geradoEmUtc = DateTime.UtcNow,   // permite ver quando a resposta veio do cache
        porSituacao = await db.Tarefas.AsNoTracking().GroupBy(t => t.Situacao)
            .Select(g => new { situacao = g.Key, quantidade = g.Count() }).ToListAsync(ct)
    }))
    .AllowAnonymous()
    .CacheOutput("EstatisticasPublicas");

// Dica do serviço externo (com retry, circuit breaker e fallback)
app.MapGet("/api/v1/dica", (ClienteDicas dicas, CancellationToken ct) => dicas.ObterDicaAsync(ct))
    .RequireAuthorization(Politicas.LeituraPrivada);

if (app.Environment.IsDevelopment()) app.MapSimuladorDicas();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                 // /openapi/v1.json
    app.MapScalarApiReference();      // /scalar/v1 – interface interativa só em desenvolvimento
}

// Painel: mesma leitura com EF Core e com Dapper, para comparação
app.MapGet("/api/painel", async (string? fonte, PainelConsultas painel, CancellationToken ct) =>
{
    var hoje = DateOnly.FromDateTime(DateTime.Today);
    var linhas = fonte == "dapper" ? await painel.ComDapperAsync(hoje, ct) : await painel.ComEfCoreAsync(hoje, ct);
    return Results.Ok(new { fonte = fonte ?? "ef", linhas });
}).RequireAuthorization(Politicas.LeituraPrivada);

app.MapControllers();                                   // controllers com [Route] (API de diagnóstico)
app.MapControllerRoute(                                  // rota convencional da interface MVC
    name: "default",
    pattern: "{controller=Tarefas}/{action=Index}/{id?}");

app.Run();

public partial class Program;   // permite referenciar a aplicação em testes de integração
