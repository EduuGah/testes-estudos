using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Contratos;
using PortalTarefas.Web.Seguranca;
using PortalTarefas.Web.Servicos;

namespace PortalTarefas.Web.Endpoints;

/// <summary>
/// API REST v1 do recurso "tarefas". Contrato definido antes da implementação:
///   GET    /api/v1/tarefas          200 (página)        400 (parâmetros)
///   GET    /api/v1/tarefas/{id}     200                 404
///   POST   /api/v1/tarefas          201 + Location      400
///   PUT    /api/v1/tarefas/{id}     200                 400 | 404 | 409 (concorrência)
///   DELETE /api/v1/tarefas/{id}     204                 404
/// Erros sempre em Problem Details (application/problem+json).
/// </summary>
public static class TarefasApiV1
{
    public static RouteGroupBuilder MapTarefasApiV1(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/tarefas").WithTags("Tarefas v1")
            .RequireAuthorization(Politicas.LeituraPrivada);   // Unidade 6: leitura privada → 401 para anônimo

        grupo.MapGet("/", Listar)
            .WithName("ListarTarefas")
            .WithSummary("Lista tarefas com paginação, ordenação e filtros")
            .WithDescription("Filtros e ordenação são aplicados no banco antes da materialização. Tamanho máximo de página: 50.");

        grupo.MapGet("/{id:int}", Obter)
            .WithName("ObterTarefaV1")
            .WithSummary("Obtém os detalhes de uma tarefa");

        grupo.MapPost("/", Criar)
            .WithName("CriarTarefa")
            .WithSummary("Cria uma tarefa e devolve Location")
            .RequireAuthorization(Politicas.CriarTarefa)
            .RequireRateLimiting("escrita");

        grupo.MapPut("/{id:int}", Atualizar)
            .WithName("AtualizarTarefa")
            .WithSummary("Atualiza uma tarefa usando concorrência otimista")
            .WithDescription("Envie em 'versao' o valor recebido no GET. Se outra pessoa alterou antes, a resposta é 409.")
            .RequireRateLimiting("escrita");

        grupo.MapDelete("/{id:int}", Excluir)
            .WithName("ExcluirTarefa")
            .WithSummary("Exclui uma tarefa")
            .RequireAuthorization(Politicas.ExcluirTarefa)
            .RequireRateLimiting("escrita");

        return grupo;
    }

    private static async Task<Results<Ok<PaginaDto<TarefaResumoDto>>, ValidationProblem>> Listar(
        [AsParameters] ConsultaTarefasQuery query, TarefasApiServico servico, CancellationToken ct)
    {
        var consulta = query.ParaConsulta();
        var erros = consulta.Validar();
        if (erros.Count > 0) return TypedResults.ValidationProblem(erros, title: "Parâmetros de consulta inválidos");
        return TypedResults.Ok(await servico.ConsultarAsync(consulta, ct));
    }

    private static async Task<Results<Ok<TarefaDetalheDto>, ProblemHttpResult>> Obter(int id, TarefasApiServico servico, CancellationToken ct) =>
        await servico.ObterAsync(id, ct) is { } dto
            ? TypedResults.Ok(dto)
            : NaoEncontrada(id);

    private static async Task<Results<CreatedAtRoute<TarefaDetalheDto>, ValidationProblem>> Criar(
        CriarTarefaDto dto, ClaimsPrincipal usuario, TarefasApiServico servico, CancellationToken ct)
    {
        var erros = TarefasApiServico.ValidarDto(dto);
        if (erros.Count > 0) return TypedResults.ValidationProblem(erros);
        var criada = await servico.CriarAsync(dto, usuario, ct);
        return TypedResults.CreatedAtRoute(criada, "ObterTarefaV1", new { id = criada.Id });
    }

    private static async Task<Results<Ok<TarefaDetalheDto>, ValidationProblem, ProblemHttpResult, ForbidHttpResult>> Atualizar(
        int id, AtualizarTarefaDto dto, ClaimsPrincipal usuario, IAuthorizationService autorizacao,
        ITarefaServico tarefas, TarefasApiServico servico, CancellationToken ct)
    {
        // Autorização baseada no recurso: a decisão depende da equipe/proprietário desta tarefa
        var atual = await tarefas.ObterAsync(id, ct);
        if (atual is null) return NaoEncontrada(id);
        if (!(await autorizacao.AuthorizeAsync(usuario, atual, Politicas.EditarTarefa)).Succeeded)
            return TypedResults.Forbid();   // autenticado, mas sem permissão → 403

        var erros = TarefasApiServico.ValidarDto(dto);
        if (!Modelos.SituacaoTarefa.Todas.Contains(dto.Situacao)) erros["situacao"] = ["Situação inválida."];
        if (erros.Count > 0) return TypedResults.ValidationProblem(erros);
        try
        {
            return await servico.AtualizarAsync(id, dto, ct) is { } atualizada ? TypedResults.Ok(atualizada) : NaoEncontrada(id);
        }
        catch (ConflitoConcorrenciaException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflito de concorrência",
                detail: "A tarefa foi alterada por outra sessão. Faça um novo GET, revise e envie a nova versão.",
                extensions: new Dictionary<string, object?> { ["tarefaId"] = id });
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Excluir(int id, TarefasApiServico servico, CancellationToken ct) =>
        await servico.ExcluirAsync(id, ct) ? TypedResults.NoContent() : NaoEncontrada(id);

    private static ProblemHttpResult NaoEncontrada(int id) =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Tarefa não encontrada",
            detail: $"Não existe tarefa com id {id}.");
}

/// <summary>Vincula os parâmetros de query string com os nomes do contrato.</summary>
public record ConsultaTarefasQuery(
    [FromQuery(Name = "pagina")] int? Pagina,
    [FromQuery(Name = "tamanho")] int? Tamanho,
    [FromQuery(Name = "ordenarPor")] string? OrdenarPor,
    [FromQuery(Name = "direcao")] string? Direcao,
    [FromQuery(Name = "situacao")] string? Situacao,
    [FromQuery(Name = "prioridade")] Dominio.Prioridade? Prioridade,
    [FromQuery(Name = "busca")] string? Busca)
{
    public ConsultaTarefas ParaConsulta() => new()
    {
        Pagina = Pagina ?? 1,
        Tamanho = Tamanho ?? ConsultaTarefas.TamanhoPadrao,
        OrdenarPor = OrdenarPor ?? "id",
        Direcao = (Direcao ?? "asc").ToLowerInvariant(),
        Situacao = Situacao,
        Prioridade = Prioridade,
        Busca = Busca
    };
}
