using System.Diagnostics;

namespace PortalTarefas.Web.Middleware;

/// <summary>
/// Registra método, caminho, status e duração de cada requisição.
/// Não grava corpo, query string nem cabeçalhos para evitar dados sensíveis.
/// </summary>
public class RegistroRequisicaoMiddleware(RequestDelegate proximo, ILogger<RegistroRequisicaoMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        var cronometro = Stopwatch.StartNew();
        logger.LogInformation("[2-Registro] ENTRADA {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);
        try
        {
            await proximo(contexto);
        }
        finally
        {
            cronometro.Stop();
            logger.LogInformation("[2-Registro] SAÍDA {Metodo} {Caminho} -> {Status} em {Duracao} ms",
                contexto.Request.Method, contexto.Request.Path, contexto.Response.StatusCode,
                cronometro.Elapsed.TotalMilliseconds.ToString("F1"));
        }
    }
}

/// <summary>
/// Extensão: aceita o X-Correlation-Id do cliente (se válido) ou gera um novo,
/// devolve no cabeçalho da resposta e abre um escopo de log com o identificador.
/// </summary>
public class CorrelacaoMiddleware(RequestDelegate proximo, ILogger<CorrelacaoMiddleware> logger)
{
    public const string Cabecalho = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext contexto)
    {
        var recebido = contexto.Request.Headers[Cabecalho].ToString();
        var id = Guid.TryParse(recebido, out _) ? recebido : Guid.NewGuid().ToString("N")[..12];
        contexto.TraceIdentifier = id;
        contexto.Response.OnStarting(() =>
        {
            contexto.Response.Headers[Cabecalho] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            logger.LogInformation("[1-Correlação] ENTRADA id={CorrelationId}", id);
            await proximo(contexto);
            logger.LogInformation("[1-Correlação] SAÍDA id={CorrelationId}", id);
        }
    }
}

public static class MiddlewareExtensions
{
    /// <summary>Middleware reutilizável registrado por extensão de IApplicationBuilder.</summary>
    public static IApplicationBuilder UseRegistroRequisicao(this IApplicationBuilder app) =>
        app.UseMiddleware<RegistroRequisicaoMiddleware>();

    public static IApplicationBuilder UseCorrelacao(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelacaoMiddleware>();
}
