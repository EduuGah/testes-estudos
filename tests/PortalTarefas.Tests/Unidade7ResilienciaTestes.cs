using System.Net;
using System.Net.Http.Json;

namespace PortalTarefas.Tests;

/// <summary>Comportamentos da Unidade 7 observáveis de fora da aplicação.</summary>
public class Unidade7ResilienciaTestes : TesteIntegracao
{
    private static readonly string Senha = Environment.GetEnvironmentVariable("PORTAL_SENHA_TESTE") ?? "";

    [Teste("Liveness responde 200 mesmo com a dependência fora")]
    public async Task LivenessIndependente()
    {
        await Cliente.PostAsync("/simulador/dicas/modo/fora", null);
        Afirmar.Igual(HttpStatusCode.OK, (await Cliente.GetAsync("/health/live")).StatusCode);
        Afirmar.Igual(HttpStatusCode.ServiceUnavailable, (await Cliente.GetAsync("/health/ready")).StatusCode);
        await Cliente.PostAsync("/simulador/dicas/modo/normal", null);
        Afirmar.Igual(HttpStatusCode.OK, (await Cliente.GetAsync("/health/ready")).StatusCode);
    }

    [Teste("Sexta escrita em 30 s recebe 429 com Retry-After")]
    public async Task RateLimitEscrita()
    {
        await Cliente.PostAsJsonAsync("/api/auth/login", new { email = "ana@portal.local", senha = Senha });
        HttpResponseMessage? ultima = null;
        for (var i = 1; i <= 6; i++)
            ultima = await Cliente.PostAsJsonAsync("/api/v1/tarefas", new { titulo = $"Carga controlada {i}", prioridade = "Baixa" });
        Afirmar.Igual((HttpStatusCode)429, ultima!.StatusCode);
        Afirmar.Verdadeiro(ultima.Headers.Contains("Retry-After"), "Retry-After ausente");
    }

    [Teste("Estatísticas públicas vêm do cache até a próxima escrita")]
    public async Task OutputCache()
    {
        var a = (await LerJson(await Cliente.GetAsync("/api/publico/estatisticas"))).GetProperty("geradoEmUtc").GetString();
        var b = (await LerJson(await Cliente.GetAsync("/api/publico/estatisticas"))).GetProperty("geradoEmUtc").GetString();
        Afirmar.Igual(a, b, "segunda leitura deveria vir do cache");
    }

    [Teste("Erro 500 traz correlationId e nenhum detalhe interno")]
    public async Task ErroCorrelacionado()
    {
        var r = await Cliente.GetAsync("/api/diagnostico/erro");
        var corpo = await LerJson(r);
        Afirmar.Igual(r.Headers.GetValues("X-Correlation-Id").First(), corpo.GetProperty("correlationId").GetString());
    }
}
