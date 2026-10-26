using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Integracoes;

namespace PortalTarefas.Web.Observabilidade;

/// <summary>Configuração da Unidade 7 reunida em um só lugar para manter o Program.cs legível.</summary>
public static class ObservabilidadeExtensions
{
    public static WebApplicationBuilder AdicionarObservabilidadeEResiliencia(this WebApplicationBuilder builder)
    {
        var s = builder.Services;

        // ---------- Logs estruturados ----------
        builder.Logging.ClearProviders();
        if (builder.Environment.IsDevelopment())
            builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.IncludeScopes = true; o.TimestampFormat = "HH:mm:ss "; });
        else
            builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);   // propriedades nomeadas para o coletor

        // ---------- Métricas e traces (OpenTelemetry) ----------
        s.AddSingleton<Telemetria>();
        s.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("PortalTarefas.Web", serviceVersion: "1.0.0"))
            .WithTracing(t => t
                .AddSource(Telemetria.Nome)
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(m => m
                .AddMeter(Telemetria.Nome)
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter((_, leitor) => leitor.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 30_000));

        // ---------- Dependência HTTP com resiliência ----------
        s.AddHttpClient<ClienteDicas>(c => c.BaseAddress = new Uri(builder.Configuration["ServicoDicas:UrlBase"]!))
            .AddResilienceHandler("servico-dicas", pipeline =>
            {
                // 1) Tempo total máximo, somando tentativas
                pipeline.AddTimeout(TimeSpan.FromSeconds(8));

                // 2) Retry finito, com espera exponencial, apenas para falhas transitórias (5xx, 408, 429, rede)
                var retry = new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                };
                retry.DisableForUnsafeHttpMethods();   // nunca repete POST/PUT/PATCH/DELETE (não idempotentes)
                pipeline.AddRetry(retry);

                // 3) Circuit breaker: com 50% de falhas em pelo menos 6 chamadas em 30 s, abre por 15 s
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 6,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15)
                });

                // 4) Timeout por tentativa
                pipeline.AddTimeout(TimeSpan.FromSeconds(2));
            });
        s.AddHttpClient("saude");

        // ---------- Health Checks ----------
        s.AddHealthChecks()
            .AddDbContextCheck<PortalTarefasDbContext>("banco", tags: ["ready"])
            .AddCheck<ServicoDicasHealthCheck>("servico-dicas", tags: ["ready"]);

        // ---------- Rate limiting ----------
        s.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Escrita: 5 operações a cada 30 s por usuário (ou por IP, se anônimo)
            o.AddPolicy("escrita", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromSeconds(30), QueueLimit = 0 }));

            // Login: 5 tentativas por minuto por IP (força bruta). Configurável para ambientes de teste
            // automatizado, em que vários usuários entram a partir do mesmo IP (Portal:LimiteLoginPorMinuto).
            var limiteLogin = builder.Configuration.GetValue("Portal:LimiteLoginPorMinuto", 5);
            o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteLogin, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

            o.OnRejected = async (contexto, ct) =>
            {
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                    contexto.HttpContext.Response.Headers.RetryAfter = ((int)espera.TotalSeconds).ToString();
                contexto.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiting")
                    .LogWarning("Limite excedido em {Caminho}", contexto.HttpContext.Request.Path);
                await Results.Problem(statusCode: 429, title: "Muitas requisições",
                    detail: "Limite de requisições excedido. Aguarde e tente novamente.").ExecuteAsync(contexto.HttpContext);
            };
        });

        // ---------- Output caching ----------
        s.AddOutputCache(o => o.AddPolicy("EstatisticasPublicas", p => p
            .Expire(TimeSpan.FromSeconds(30))
            .Tag("tarefas")));   // invalidado por EvictByTagAsync("tarefas") após qualquer escrita

        return builder;
    }

    /// <summary>Liveness não consulta dependências; readiness consulta banco e serviço de dicas.</summary>
    public static void MapSaude(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("ready"),
            ResponseWriter = EscreverResumo   // só status por verificação: sem mensagens internas, strings de conexão ou exceções
        }).AllowAnonymous();
    }

    private static Task EscreverResumo(HttpContext ctx, HealthReport relatorio)
    {
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = relatorio.Status.ToString(),
            verificacoes = relatorio.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
        }));
    }
}
