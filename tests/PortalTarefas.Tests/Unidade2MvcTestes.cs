using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Validacao;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Tests;

/// <summary>Testes unitários da validação customizada (não precisam do servidor).</summary>
public class Unidade2ValidacaoTestes
{
    private static readonly DateOnly Hoje = new(2026, 9, 5);

    private static List<ValidationResult> Validar(TarefaInputModel m)
    {
        var atributo = new PrazoCompativelAttribute { Hoje = () => Hoje };
        var r = atributo.GetValidationResult(m, new ValidationContext(m));
        return r is null ? [] : [r];
    }

    [Teste("Alta prioridade sem prazo é rejeitada")]
    public Task AltaSemPrazo()
    {
        var erros = Validar(new TarefaInputModel { Titulo = "X", Prioridade = Prioridade.Alta, Prazo = null });
        Afirmar.Igual(1, erros.Count);
        Afirmar.Igual("Prazo", erros[0].MemberNames.Single());
        return Task.CompletedTask;
    }

    [Teste("Alta prioridade com prazo acima de 7 dias é rejeitada")]
    public Task AltaPrazoLongo()
    {
        Afirmar.Igual(1, Validar(new TarefaInputModel { Titulo = "X", Prioridade = Prioridade.Alta, Prazo = Hoje.AddDays(8) }).Count);
        return Task.CompletedTask;
    }

    [Teste("Prazo no passado é rejeitado no cadastro")]
    public Task PrazoPassado()
    {
        Afirmar.Igual(1, Validar(new TarefaInputModel { Titulo = "X", Prioridade = Prioridade.Baixa, Prazo = Hoje.AddDays(-1) }).Count);
        return Task.CompletedTask;
    }

    [Teste("Combinações válidas passam")]
    public Task Validas()
    {
        Afirmar.Igual(0, Validar(new TarefaInputModel { Titulo = "X", Prioridade = Prioridade.Alta, Prazo = Hoje.AddDays(7) }).Count);
        Afirmar.Igual(0, Validar(new TarefaInputModel { Titulo = "X", Prioridade = Prioridade.Baixa, Prazo = null }).Count);
        return Task.CompletedTask;
    }
}

/// <summary>Testes de integração da interface MVC.</summary>
public class Unidade2MvcTestes : TesteIntegracao
{
    private async Task<(string token, string html)> AbrirFormulario(string url)
    {
        var html = await Cliente.GetStringAsync(url);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        return (token, html);
    }

    private Task<HttpResponseMessage> Enviar(string url, string token, Dictionary<string, string> campos)
    {
        campos["__RequestVerificationToken"] = token;
        return Cliente.PostAsync(url, new FormUrlEncodedContent(campos));
    }

    [Teste("Cadastro válido redireciona para os detalhes (302)")]
    public async Task CadastroValido()
    {
        await EntrarAsync();
        var (token, _) = await AbrirFormulario("/Tarefas/Create");
        var r = await Enviar("/Tarefas/Create", token, new() { ["Titulo"] = "Criada pelo teste MVC", ["Prioridade"] = "1", ["Situacao"] = "Pendente" });
        Afirmar.Igual(HttpStatusCode.Redirect, r.StatusCode);
        Afirmar.Verdadeiro(r.Headers.Location!.ToString().StartsWith("/Tarefas/Details/"), "redirecionamento inesperado");
    }

    [Teste("Cadastro com título vazio volta à view com mensagem e preserva dados")]
    public async Task CadastroTituloVazio()
    {
        await EntrarAsync();
        var (token, _) = await AbrirFormulario("/Tarefas/Create");
        var r = await Enviar("/Tarefas/Create", token, new() { ["Titulo"] = "", ["Descricao"] = "texto preservado", ["Prioridade"] = "2", ["Situacao"] = "Pendente" });
        var html = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        Afirmar.Igual(HttpStatusCode.OK, r.StatusCode);
        Afirmar.Verdadeiro(html.Contains("Informe o título da tarefa."), "mensagem ausente");
        Afirmar.Verdadeiro(html.Contains("texto preservado"), "dado digitado perdido");
    }

    [Teste("Validação customizada de prazo aparece no formulário")]
    public async Task ValidacaoPrazo()
    {
        await EntrarAsync();
        var (token, _) = await AbrirFormulario("/Tarefas/Create");
        var prazo = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
        var r = await Enviar("/Tarefas/Create", token, new() { ["Titulo"] = "Urgente", ["Prioridade"] = "3", ["Prazo"] = prazo, ["Situacao"] = "Pendente" });
        Afirmar.Verdadeiro(WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()).Contains("no máximo 7 dias"), "regra de prazo não aplicada");
    }

    [Teste("Identificador existente (200) e inexistente (404)")]
    public async Task ExistenteEInexistente()
    {
        await EntrarAsync();
        Afirmar.Igual(HttpStatusCode.OK, (await Cliente.GetAsync("/Tarefas/Details/1")).StatusCode);
        Afirmar.Igual(HttpStatusCode.NotFound, (await Cliente.GetAsync("/Tarefas/Details/999")).StatusCode);
    }

    [Teste("Três páginas compartilham o mesmo layout e o View Component")]
    public async Task LayoutConsistente()
    {
        await EntrarAsync();
        foreach (var url in new[] { "/Tarefas", "/Tarefas/Details/1", "/Tarefas/Create" })
        {
            var html = await Cliente.GetStringAsync(url);
            Afirmar.Verdadeiro(html.Contains("class=\"topo\"") && html.Contains("Tarefas vencidas") && html.Contains("<footer>"), $"layout ausente em {url}");
        }
    }
}
