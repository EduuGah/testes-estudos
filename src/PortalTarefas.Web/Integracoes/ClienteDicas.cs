using System.Diagnostics;
using Polly.CircuitBreaker;
using PortalTarefas.Web.Observabilidade;

namespace PortalTarefas.Web.Integracoes;

public record DicaDto(string Texto, string Origem);

/// <summary>
/// Cliente da dependência HTTP "Serviço de Dicas" (sugestões de produtividade exibidas no painel).
/// A resiliência (timeout, retry e circuit breaker) é configurada no Program.cs via AddResilienceHandler.
/// Quando tudo falha, devolve um fallback claro em vez de quebrar a tela.
/// </summary>
public class ClienteDicas(HttpClient http, Telemetria telemetria, ILogger<ClienteDicas> logger)
{
    public static readonly DicaDto Fallback = new("Divida tarefas grandes em entregas menores.", "fallback");

    public async Task<DicaDto> ObterDicaAsync(CancellationToken ct)
    {
        using var span = Telemetria.Fonte.StartActivity("ServicoDicas.ObterDica", ActivityKind.Client);
        var cronometro = Stopwatch.StartNew();
        var resultado = "sucesso";
        try
        {
            var dica = await http.GetFromJsonAsync<DicaDto>("dicas/aleatoria", ct);
            return dica ?? Fallback;
        }
        catch (BrokenCircuitException)
        {
            resultado = "circuito_aberto";
            logger.LogWarning("Serviço de dicas com circuito aberto; usando fallback");
            span?.SetStatus(ActivityStatusCode.Error, "circuito aberto");
            return Fallback;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or Polly.Timeout.TimeoutRejectedException)
        {
            resultado = "falha";
            logger.LogWarning("Serviço de dicas indisponível ({Tipo}); usando fallback", ex.GetType().Name);
            span?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            return Fallback;
        }
        finally
        {
            telemetria.DuracaoServicoDicas.Record(cronometro.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("resultado", resultado));
            span?.SetTag("portal.dicas.resultado", resultado);
        }
    }
}
