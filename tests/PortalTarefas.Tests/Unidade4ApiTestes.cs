using System.Net;

namespace PortalTarefas.Tests;

/// <summary>Contrato da API v1: paginação, ordenação, filtros e Problem Details.</summary>
public class Unidade4ApiTestes : TesteIntegracao
{
    [Teste("Tamanho de página fora de 1..50 retorna 400")]
    public async Task LimitesPaginacao()
    {
        await EntrarAsync();
        Afirmar.Igual(HttpStatusCode.BadRequest, (await Cliente.GetAsync("/api/v1/tarefas?tamanho=0")).StatusCode);
        Afirmar.Igual(HttpStatusCode.BadRequest, (await Cliente.GetAsync("/api/v1/tarefas?tamanho=51")).StatusCode);
        Afirmar.Igual(HttpStatusCode.OK, (await Cliente.GetAsync("/api/v1/tarefas?tamanho=50")).StatusCode);
    }

    [Teste("Ordenação aceita só campos da lista permitida")]
    public async Task Ordenacao()
    {
        await EntrarAsync();
        Afirmar.Igual(HttpStatusCode.OK, (await Cliente.GetAsync("/api/v1/tarefas?ordenarPor=prazo&direcao=desc")).StatusCode);
        var r = await Cliente.GetAsync("/api/v1/tarefas?ordenarPor=descricao");
        Afirmar.Igual(HttpStatusCode.BadRequest, r.StatusCode);
        Afirmar.Igual("application/problem+json", r.Content.Headers.ContentType?.MediaType);
    }

    [Teste("Dois filtros combinados e página vazia")]
    public async Task FiltrosEPaginaVazia()
    {
        await EntrarAsync();
        var pagina = await LerJson(await Cliente.GetAsync("/api/v1/tarefas?situacao=EmAndamento&prioridade=Alta"));
        foreach (var t in pagina.GetProperty("itens").EnumerateArray())
        {
            Afirmar.Igual("EmAndamento", t.GetProperty("situacao").GetString());
            Afirmar.Igual("Alta", t.GetProperty("prioridade").GetString());
        }
        var vazia = await LerJson(await Cliente.GetAsync("/api/v1/tarefas?pagina=999"));
        Afirmar.Igual(0, vazia.GetProperty("itens").GetArrayLength());
    }

    [Teste("Recurso inexistente retorna 404 em Problem Details")]
    public async Task Inexistente()
    {
        await EntrarAsync();
        var r = await Cliente.GetAsync("/api/v1/tarefas/999999");
        Afirmar.Igual(HttpStatusCode.NotFound, r.StatusCode);
        Afirmar.Igual("application/problem+json", r.Content.Headers.ContentType?.MediaType);
    }
}
