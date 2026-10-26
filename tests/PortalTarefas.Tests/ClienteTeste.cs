using System.Net.Http.Json;
using System.Text.Json;

namespace PortalTarefas.Tests;

/// <summary>Base dos testes de integração: HttpClient apontando para a API em execução.</summary>
public abstract class TesteIntegracao : IDisposable
{
    protected static readonly string BaseUrl = Environment.GetEnvironmentVariable("PORTAL_API_URL") ?? "https://localhost:7001";
    protected readonly HttpClient Cliente;

    protected TesteIntegracao()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true };   // guarda o cookie de login
        // Somente em teste local: aceita o certificado de desenvolvimento do ASP.NET Core
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.Subject == "CN=localhost";
        Cliente = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
    }

    /// <summary>A partir da Unidade 6 a leitura é privada: os testes entram com um usuário de seed.</summary>
    protected async Task EntrarAsync(string email = "admin@portal.local")
    {
        var senha = Environment.GetEnvironmentVariable("PORTAL_SENHA_TESTE") ?? "";
        var r = await Cliente.PostAsJsonAsync("/api/auth/login", new { email, senha });
        r.EnsureSuccessStatusCode();
    }

    protected static async Task<JsonElement> LerJson(HttpResponseMessage resposta) =>
        await resposta.Content.ReadFromJsonAsync<JsonElement>();

    public void Dispose() => Cliente.Dispose();
}
