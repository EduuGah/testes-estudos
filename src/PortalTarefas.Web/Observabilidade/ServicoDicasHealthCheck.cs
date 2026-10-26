using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PortalTarefas.Web.Observabilidade;

/// <summary>Readiness da dependência essencial "Serviço de Dicas".</summary>
public class ServicoDicasHealthCheck(IHttpClientFactory fabrica, IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var http = fabrica.CreateClient("saude");
            http.Timeout = TimeSpan.FromSeconds(2);
            var r = await http.GetAsync(new Uri(new Uri(config["ServicoDicas:UrlBase"]!), "saude"), ct);
            return r.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"status {(int)r.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.GetType().Name);   // sem mensagem interna
        }
    }
}
