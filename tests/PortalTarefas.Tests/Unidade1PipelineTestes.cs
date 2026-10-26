using System.Net;
using System.Net.Http.Json;

namespace PortalTarefas.Tests;

public class Unidade1PipelineTestes : TesteIntegracao
{
    [Teste("Rota válida retorna 200 e a tarefa")]
    public async Task RotaValida()
    {
        await EntrarAsync();
        var r = await Cliente.GetAsync("/api/tarefas/1");
        Afirmar.Igual(HttpStatusCode.OK, r.StatusCode);
        Afirmar.Igual(1, (await LerJson(r)).GetProperty("id").GetInt32());
    }

    [Teste("Id inexistente retorna 404 em Problem Details")]
    public async Task RotaInexistente()
    {
        await EntrarAsync();
        var r = await Cliente.GetAsync("/api/tarefas/9999");
        Afirmar.Igual(HttpStatusCode.NotFound, r.StatusCode);
        Afirmar.Igual("application/problem+json", r.Content.Headers.ContentType?.MediaType);
    }

    [Teste("Id não numérico não casa com a restrição {id:int}")]
    public async Task RotaNaoNumerica()
    {
        await EntrarAsync();
        var r = await Cliente.GetAsync("/api/tarefas/abc");
        Afirmar.Igual(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Teste("Filtro por query string e cabeçalho opcional")]
    public async Task FiltroQueryECabecalho()
    {
        await EntrarAsync();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/tarefas?situacao=Concluida");
        req.Headers.Add("X-Cliente-Id", "teste-automatizado");
        var r = await Cliente.SendAsync(req);
        Afirmar.Igual(HttpStatusCode.OK, r.StatusCode);
        foreach (var t in (await LerJson(r)).EnumerateArray())
            Afirmar.Igual("Concluida", t.GetProperty("situacao").GetString());
    }

    [Teste("POST válido retorna 201 com cabeçalho Location")]
    public async Task PostValido()
    {
        await EntrarAsync();
        var r = await Cliente.PostAsJsonAsync("/api/tarefas", new { titulo = "Tarefa criada pelo teste", situacao = "Pendente" });
        Afirmar.Igual(HttpStatusCode.Created, r.StatusCode);
        Afirmar.Verdadeiro(r.Headers.Location is not null, "Location ausente");
    }

    [Teste("POST inválido retorna 400 com erros por campo")]
    public async Task PostInvalido()
    {
        await EntrarAsync();
        var r = await Cliente.PostAsJsonAsync("/api/tarefas", new { titulo = "", situacao = "Atrasada" });
        Afirmar.Igual(HttpStatusCode.BadRequest, r.StatusCode);
        var erros = (await LerJson(r)).GetProperty("errors");
        Afirmar.Verdadeiro(erros.TryGetProperty("titulo", out _), "erro de título ausente");
    }

    [Teste("Exceção vira 500 sem detalhes internos")]
    public async Task ExcecaoControlada()
    {
        var r = await Cliente.GetAsync("/api/diagnostico/erro");
        var corpo = await r.Content.ReadAsStringAsync();
        Afirmar.Igual(HttpStatusCode.InternalServerError, r.StatusCode);
        Afirmar.Verdadeiro(!corpo.Contains("repositório") && !corpo.Contains("StackTrace"), "detalhe interno exposto");
    }

    [Teste("CORS libera somente a origem do front-end local")]
    public async Task Cors()
    {
        async Task<string?> Preflight(string origem)
        {
            var req = new HttpRequestMessage(HttpMethod.Options, "/api/tarefas");
            req.Headers.Add("Origin", origem);
            req.Headers.Add("Access-Control-Request-Method", "POST");
            var r = await Cliente.SendAsync(req);
            return r.Headers.TryGetValues("Access-Control-Allow-Origin", out var v) ? v.First() : null;
        }
        Afirmar.Igual("http://localhost:5173", await Preflight("http://localhost:5173"));
        Afirmar.Igual(null, await Preflight("http://site-malicioso.com"));
    }

    [Teste("Correlation-Id é devolvido e reaproveitado quando enviado")]
    public async Task Correlacao()
    {
        var id = Guid.NewGuid().ToString();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/diagnostico");
        req.Headers.Add("X-Correlation-Id", id);
        var r = await Cliente.SendAsync(req);
        Afirmar.Igual(id, r.Headers.GetValues("X-Correlation-Id").First());
    }

    [Teste("Minimal API e controller MVC retornam o mesmo contrato")]
    public async Task MinimalEquivaleMvc()
    {
        var a = await LerJson(await Cliente.GetAsync("/api/diagnostico"));
        var b = await LerJson(await Cliente.GetAsync("/api/mvc/diagnostico"));
        Afirmar.Igual(a.GetProperty("totalTarefas").GetInt32(), b.GetProperty("totalTarefas").GetInt32());
    }
}
