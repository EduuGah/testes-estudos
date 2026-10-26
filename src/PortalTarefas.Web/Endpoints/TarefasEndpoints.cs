using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Modelos;
using PortalTarefas.Web.Servicos;

namespace PortalTarefas.Web.Endpoints;

public static class TarefasEndpoints
{
    public static RouteGroupBuilder MapTarefasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/tarefas").WithTags("Tarefas");

        // GET /api/tarefas?situacao=Pendente&busca=api   (query string + cabeçalho opcional)
        grupo.MapGet("/", (
            [FromQuery] string? situacao,
            [FromQuery] string? busca,
            [FromHeader(Name = "X-Cliente-Id")] string? clienteId,
            RepositorioTarefasMemoria repositorio,
            ILogger<RepositorioTarefasMemoria> logger) =>
        {
            if (situacao is not null && !SituacaoTarefa.Todas.Contains(situacao, StringComparer.OrdinalIgnoreCase))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["situacao"] = [$"Situação inválida. Use: {string.Join(", ", SituacaoTarefa.Todas)}."]
                });
            }
            logger.LogInformation("[Endpoint] Listando tarefas para o cliente {Cliente}", clienteId ?? "(não informado)");
            return Results.Ok(repositorio.Listar(situacao, busca));
        });

        // GET /api/tarefas/2   (id vinculado pela rota; a restrição :int rejeita "abc")
        grupo.MapGet("/{id:int}", (int id, RepositorioTarefasMemoria repositorio) =>
            repositorio.Obter(id) is { } tarefa
                ? Results.Ok(tarefa)
                : Results.Problem(statusCode: 404, title: "Tarefa não encontrada", detail: $"Não existe tarefa com id {id}."))
            .WithName("ObterTarefa");

        // POST /api/tarefas   (corpo JSON)
        grupo.MapPost("/", ([FromBody] CriarTarefaRequest req, RepositorioTarefasMemoria repositorio) =>
        {
            var erros = Validar(req);
            if (erros.Count > 0) return Results.ValidationProblem(erros);
            var criada = repositorio.Adicionar(req);
            return Results.CreatedAtRoute("ObterTarefa", new { id = criada.Id }, criada);
        });

        return grupo;
    }

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
