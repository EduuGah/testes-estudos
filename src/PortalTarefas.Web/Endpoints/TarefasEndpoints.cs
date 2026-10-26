using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Modelos;
using PortalTarefas.Web.Servicos;

namespace PortalTarefas.Web.Endpoints;

public static class TarefasEndpoints
{
    public static RouteGroupBuilder MapTarefasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/tarefas").WithTags("Tarefas");

        // GET /api/tarefas?situacao=Pendente&busca=api   (query string + cabeçalho opcional)
        grupo.MapGet("/", async (
            [FromQuery] string? situacao,
            [FromQuery] string? busca,
            [FromHeader(Name = "X-Cliente-Id")] string? clienteId,
            ITarefaServico servico,
            ILoggerFactory logs,
            CancellationToken ct) =>
        {
            if (situacao is not null && !SituacaoTarefa.Todas.Contains(situacao, StringComparer.OrdinalIgnoreCase))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["situacao"] = [$"Situação inválida. Use: {string.Join(", ", SituacaoTarefa.Todas)}."]
                });
            }
            logs.CreateLogger("TarefasEndpoints").LogInformation("[Endpoint] Listando tarefas para o cliente {Cliente}", clienteId ?? "(não informado)");
            return Results.Ok((await servico.ListarAsync(situacao, busca, ct)).Select(ParaDto));
        });

        // GET /api/tarefas/2   (id vinculado pela rota; a restrição :int rejeita "abc")
        grupo.MapGet("/{id:int}", async (int id, ITarefaServico servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } tarefa
                ? Results.Ok(ParaDto(tarefa))
                : Results.Problem(statusCode: 404, title: "Tarefa não encontrada", detail: $"Não existe tarefa com id {id}."))
            .WithName("ObterTarefa");

        // POST /api/tarefas   (corpo JSON)
        grupo.MapPost("/", async ([FromBody] CriarTarefaRequest req, ITarefaServico servico, CancellationToken ct) =>
        {
            var erros = Validar(req);
            if (erros.Count > 0) return Results.ValidationProblem(erros);
            var criada = await servico.CriarAsync(new Tarefa { Titulo = req.Titulo.Trim(), Descricao = req.Descricao, Situacao = req.Situacao }, ct);
            return Results.CreatedAtRoute("ObterTarefa", new { id = criada.Id }, ParaDto(criada));
        });

        return grupo;
    }

    private static TarefaDto ParaDto(Tarefa t) => new(t.Id, t.Titulo, t.Descricao, t.Situacao);

    private static Dictionary<string, string[]> Validar(CriarTarefaRequest req)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(req, new ValidationContext(req), resultados, validateAllProperties: true);
        if (!SituacaoTarefa.Todas.Contains(req.Situacao))
            resultados.Add(new ValidationResult("Situação inválida.", [nameof(req.Situacao)]));
        return resultados
            .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(g => char.ToLowerInvariant(g.Key[0]) + g.Key[1..], g => g.Select(r => r.ErrorMessage!).ToArray());
    }
}
