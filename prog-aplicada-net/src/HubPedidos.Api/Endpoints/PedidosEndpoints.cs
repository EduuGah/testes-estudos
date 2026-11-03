using System.Security.Claims;
using HubPedidos.Api.TempoReal;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Fila;
using HubPedidos.Application.Importacao;
using HubPedidos.Domain;
using HubPedidos.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HubPedidos.Api.Endpoints;

public static class PedidosEndpoints
{
    public const string VersoesSuportadas = "1, 2";
    public const string VersoesDepreciadas = "1";

    public static void MapPedidos(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1").WithGroupName("v1").RequireAuthorization()
            .AddEndpointFilter(async (ctx, next) =>
            {
                var r = ctx.HttpContext.Response;
                r.Headers["api-supported-versions"] = VersoesSuportadas;
                r.Headers["api-deprecated-versions"] = VersoesDepreciadas;
                r.Headers["Deprecation"] = "@1798761600";                  // 01/01/2027 (RFC 9745)
                r.Headers["Sunset"] = "Sat, 01 Jan 2028 00:00:00 GMT";     // RFC 8594
                return await next(ctx);
            });
        var v2 = app.MapGroup("/api/v2").WithGroupName("v2").RequireAuthorization()
            .AddEndpointFilter(async (ctx, next) =>
            {
                ctx.HttpContext.Response.Headers["api-supported-versions"] = VersoesSuportadas;
                return await next(ctx);
            });

        v1.MapPost("/pedidos", CriarAsync);
        v1.MapGet("/pedidos/{id:guid}", (Guid id, HttpContext h, IPedidoRepositorio r, CancellationToken ct) => ObterAsync(id, h, r, Representacoes.V1, ct));
        v2.MapGet("/pedidos/{id:guid}", (Guid id, HttpContext h, IPedidoRepositorio r, CancellationToken ct) => ObterAsync(id, h, r, Representacoes.V2, ct));
        v1.MapPut("/pedidos/{id:guid}/status", AlterarStatusAsync);
        v1.MapGet("/pedidos", ListarAsync);
        v1.MapPost("/importacoes", ImportarAsync);
        v1.MapGet("/fila/metricas", (FilaProcessamento f, MetricasFila m, LimitadorBanco l) =>
            TypedResults.Ok(new { fila = m.Snapshot(f.Profundidade), f.Capacidade, modo = f.ModoCheio.ToString(), limiteBanco = l.Limite, maximoBancoObservado = l.MaximoObservado }));

        // leitura pública e idempotente: única rota com output cache
        app.MapGet("/api/v1/estatisticas", async (IPedidoRepositorio r, ContadorEstatisticas c, HttpContext h, CancellationToken ct) =>
        {
            c.Incrementar();
            h.Response.Headers["X-Gerado-Em"] = DateTimeOffset.UtcNow.ToString("O");
            return TypedResults.Ok(await r.EstatisticasAsync(ct));
        }).WithGroupName("v1").AllowAnonymous().CacheOutput("Estatisticas");

        // versão ausente: atendida pela versão padrão (v1), com o cabeçalho informando qual versão respondeu
        app.MapGet("/api/pedidos/{id:guid}", (Guid id, HttpContext h, IPedidoRepositorio r, CancellationToken ct) =>
        {
            h.Response.Headers["api-version"] = "1";
            return ObterAsync(id, h, r, Representacoes.V1, ct);
        }).RequireAuthorization().ExcludeFromDescription();

        // versão desconhecida: 400 explícito com as versões suportadas
        app.Map("/api/v{versao:int}/{**resto}", (int versao) => versao is 1 or 2
                ? Results.NotFound()
                : Results.Problem(statusCode: 400, title: "Versão não suportada",
                    detail: $"A versão {versao} não existe. Versões suportadas: {VersoesSuportadas}."))
            .ExcludeFromDescription();
    }

    private static async Task<Results<Accepted<PedidoCriadoResponse>, ValidationProblem, ProblemHttpResult>> CriarAsync(
        CriarPedidoRequest? request, ClaimsPrincipal usuario, IPedidoRepositorio repositorio, FilaProcessamento fila,
        INotificadorPedidos notificador, CancellationToken ct)
    {
        // DTO inválido é rejeitado antes de existir objeto de domínio
        if (!PedidoMapper.TryCriar(request, usuario.Identity!.Name!, out var pedido, out var erros))
            return TypedResults.ValidationProblem(erros);

        await repositorio.AdicionarAsync(pedido!, PedidoMapper.Metadados(request!), ct);
        var categoria = ClassificadorPedidos.Classificar(pedido);

        if (!fila.TryEnfileirar(new ProcessarPedidoJob(pedido!.Id, DateTimeOffset.UtcNow)))
        {
            await repositorio.AtualizarStatusAsync(pedido.Id, StatusPedido.Cancelado, null, ct);
            return TypedResults.Problem(statusCode: 503, title: "Fila cheia",
                detail: "O hub está no limite de processamento. Tente novamente em alguns segundos.",
                extensions: new Dictionary<string, object?> { ["retryAfterSegundos"] = 2 });
        }

        await notificador.PedidoAtualizadoAsync(new PedidoAtualizadoEvento(pedido.Id.Valor, "Recebido", 1, DateTimeOffset.UtcNow), ct);
        return TypedResults.Accepted($"/api/v1/pedidos/{pedido.Id}",
            new PedidoCriadoResponse(pedido.Id.Valor, categoria.ToString(), "Recebido", pedido.Total));
    }

    private static async Task<IResult> ObterAsync<T>(Guid id, HttpContext http, IPedidoRepositorio repositorio, Func<PedidoDetalhe, T> mapear, CancellationToken ct)
    {
        var detalhe = await repositorio.ObterDetalheAsync(new PedidoId(id), ct);
        if (detalhe is null || detalhe.Cliente != http.User.Identity?.Name) return TypedResults.NotFound();   // não revela pedidos de terceiros

        var representacao = mapear(detalhe);
        var etag = Representacoes.Etag(representacao);
        http.Response.Headers.ETag = etag;
        http.Response.Headers.CacheControl = "private, no-cache";   // resposta personalizada: nunca em cache compartilhado
        if (Representacoes.Corresponde(http.Request.Headers.IfNoneMatch, etag))
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        return TypedResults.Ok(representacao);
    }

    private static async Task<IResult> AlterarStatusAsync(
        Guid id, AlterarStatusRequest request, HttpContext http, IPedidoRepositorio repositorio, INotificadorPedidos notificador, CancellationToken ct)
    {
        var detalhe = await repositorio.ObterDetalheAsync(new PedidoId(id), ct);
        if (detalhe is null || detalhe.Cliente != http.User.Identity?.Name) return TypedResults.NotFound();
        if (!Enum.TryParse<StatusPedido>(request.Status, out var novo) || novo is not (StatusPedido.Cancelado or StatusPedido.Recebido))
            return TypedResults.Problem(statusCode: 400, title: "Status inválido", detail: "O cliente só pode cancelar ou reenviar o pedido.");

        // If-Match obrigatório: atualização otimista
        var ifMatch = http.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(ifMatch))
            return TypedResults.Problem(statusCode: 428, title: "Pré-condição obrigatória", detail: "Envie If-Match com o ETag atual.");
        if (!Representacoes.Corresponde(ifMatch, Representacoes.Etag(Representacoes.V1(detalhe))))
            return TypedResults.Problem(statusCode: 412, title: "Versão desatualizada", detail: "O pedido mudou desde a sua última leitura.");

        int? versao;
        try { versao = await repositorio.AtualizarStatusAsync(new PedidoId(id), novo, detalhe.Versao, ct); }
        catch (ConflitoVersaoException) { return TypedResults.Problem(statusCode: 412, title: "Versão desatualizada"); }

        await notificador.PedidoAtualizadoAsync(new PedidoAtualizadoEvento(id, novo.ToString(), versao ?? 0, DateTimeOffset.UtcNow), ct);
        var atual = Representacoes.V1((await repositorio.ObterDetalheAsync(new PedidoId(id), ct))!);
        http.Response.Headers.ETag = Representacoes.Etag(atual);
        return TypedResults.Ok(atual);
    }

    private static async Task<Ok<PaginaDto<PedidoResumoDto>>> ListarAsync(
        string? status, int? pagina, int? tamanho, RepositorioPedidos repositorio, IConfiguration config, CancellationToken ct)
    {
        var filtro = new FiltroPainel(status, pagina ?? 1, tamanho ?? 20);
        var resultado = config["Hub:ModoPainel"] == "Ingenuo"
            ? await repositorio.ListarIngenuoAsync(filtro, ct)
            : await repositorio.ListarAsync(filtro, ct);
        return TypedResults.Ok(resultado);
    }

    private static async Task<IResult> ImportarAsync(HttpRequest request, ImportadorUtf8Pooled importador, CancellationToken ct)
    {
        const long limite = 5 * 1024 * 1024;
        long linhas = 0;
        decimal total = 0;
        try
        {
            // o corpo é lido em streaming: o lote nunca fica inteiro na memória
            linhas = await importador.ProcessarAsync(request.Body, limite, l => total += l.Quantidade * l.PrecoUnitario, ct);
        }
        catch (LoteMuitoGrandeException ex) { return TypedResults.Problem(statusCode: 413, title: "Lote muito grande", detail: ex.Message); }
        catch (LinhaInvalidaException ex) { return TypedResults.Problem(statusCode: 400, title: "Linha inválida", detail: ex.Message); }
        return TypedResults.Ok(new { linhas, total });
    }
}

public sealed class ContadorEstatisticas
{
    private int _execucoes;
    public int Execucoes => Volatile.Read(ref _execucoes);
    public void Incrementar() => Interlocked.Increment(ref _execucoes);
}
