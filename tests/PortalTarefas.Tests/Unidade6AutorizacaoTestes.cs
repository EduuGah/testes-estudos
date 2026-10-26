using System.Net;
using System.Net.Http.Json;

namespace PortalTarefas.Tests;

/// <summary>
/// Matriz 401/403/200 chamando a API diretamente, sem a SPA.
/// Requer a variável PORTAL_SENHA_TESTE com a mesma senha configurada em SeedIdentidade:Senha.
/// </summary>
public class Unidade6AutorizacaoTestes : TesteIntegracao
{
    private static readonly string Senha = Environment.GetEnvironmentVariable("PORTAL_SENHA_TESTE") ?? "";

    private async Task Entrar(string email)
    {
        var r = await Cliente.PostAsJsonAsync("/api/auth/login", new { email, senha = Senha });
        Afirmar.Igual(HttpStatusCode.OK, r.StatusCode, $"login de {email}");
    }

    [Teste("Anônimo recebe 401 na leitura privada")]
    public async Task AnonimoRecebe401() =>
        Afirmar.Igual(HttpStatusCode.Unauthorized, (await Cliente.GetAsync("/api/v1/tarefas")).StatusCode);

    [Teste("Login inválido tem a mesma mensagem para conta existente e inexistente")]
    public async Task LoginInvalidoGenerico()
    {
        var existente = await Cliente.PostAsJsonAsync("/api/auth/login", new { email = "ana@portal.local", senha = "errada" });
        var inexistente = await Cliente.PostAsJsonAsync("/api/auth/login", new { email = "ninguem@portal.local", senha = "errada" });
        Afirmar.Igual(HttpStatusCode.Unauthorized, existente.StatusCode);
        Afirmar.Igual(await existente.Content.ReadAsStringAsync().ContinueWith(t => t.Result.Contains("E-mail ou senha inválidos.")),
                      await inexistente.Content.ReadAsStringAsync().ContinueWith(t => t.Result.Contains("E-mail ou senha inválidos.")));
    }

    [Teste("Usuário de outra equipe recebe 403 ao editar")]
    public async Task OutraEquipe403()
    {
        await Entrar("bruno@portal.local");   // equipe Dados; tarefa 1 é da equipe Geral
        var detalhe = await LerJson(await Cliente.GetAsync("/api/v1/tarefas/1"));
        var r = await Cliente.PutAsJsonAsync("/api/v1/tarefas/1", new
        {
            titulo = "Tentativa", prioridade = "Alta", situacao = "Concluida", versao = detalhe.GetProperty("versao").GetString()
        });
        Afirmar.Igual(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Teste("Claim editar-outras-equipes permite editar (200)")]
    public async Task ClaimPermite()
    {
        await Entrar("carla@portal.local");
        var detalhe = await LerJson(await Cliente.GetAsync("/api/v1/tarefas/1"));
        var r = await Cliente.PutAsJsonAsync("/api/v1/tarefas/1", new
        {
            titulo = detalhe.GetProperty("titulo").GetString(), prioridade = "Alta", situacao = "Concluida",
            versao = detalhe.GetProperty("versao").GetString()
        });
        Afirmar.Igual(HttpStatusCode.OK, r.StatusCode);
    }

    [Teste("Somente Administrador exclui")]
    public async Task ExclusaoSomenteAdmin()
    {
        await Entrar("ana@portal.local");
        Afirmar.Igual(HttpStatusCode.Forbidden, (await Cliente.DeleteAsync("/api/v1/tarefas/6")).StatusCode);
    }

    [Teste("Após logout o cookie deixa de valer (401)")]
    public async Task Logout()
    {
        await Entrar("ana@portal.local");
        await Cliente.PostAsync("/api/auth/logout", null);
        Afirmar.Igual(HttpStatusCode.Unauthorized, (await Cliente.GetAsync("/api/auth/me")).StatusCode);
    }
}
