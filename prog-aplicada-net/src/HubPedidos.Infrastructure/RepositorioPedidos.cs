using HubPedidos.Application.Contratos;
using HubPedidos.Domain;
using Microsoft.EntityFrameworkCore;

namespace HubPedidos.Infrastructure;

public sealed class RepositorioPedidos(HubDbContext db, TimeProvider relogio) : IPedidoRepositorio
{
    internal static long Centavos(decimal valor) => (long)Math.Round(valor * 100m, MidpointRounding.AwayFromZero);
    internal static decimal Reais(long centavos) => centavos / 100m;

    // Consulta compilada: expressão traduzida uma vez e reutilizada (rota quente: GET por id)
    private static readonly Func<HubDbContext, Guid, CancellationToken, Task<PedidoDetalhe?>> DetalheCompilado =
        EF.CompileAsyncQuery((HubDbContext ctx, Guid id, CancellationToken _) =>
            ctx.Pedidos.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new PedidoDetalhe(
                    p.Id, p.Cliente, p.Regiao, p.Prioridade, p.Status, p.TotalCentavos / 100m,
                    p.Versao, p.AtualizadoEmUtc,
                    p.Itens.OrderBy(i => i.Id).Select(i => new ItemDetalhe(i.Sku, i.Quantidade, i.PrecoCentavos / 100m)).ToList(),
                    p.Metadados.Canal))
                .FirstOrDefault());

    public async Task AdicionarAsync(Pedido pedido, MetadadosPedido metadados, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        db.Pedidos.Add(new PedidoEntidade
        {
            Id = pedido.Id.Valor,
            Cliente = pedido.Cliente,
            Regiao = pedido.Regiao.ToString(),
            Prioridade = pedido.Prioridade.ToString(),
            Status = nameof(StatusPedido.Recebido),
            TotalCentavos = Centavos(pedido.Total),
            CriadoEmUtc = agora,
            AtualizadoEmUtc = agora,
            Versao = 1,
            Itens = [.. pedido.Itens.Select(i => new ItemEntidade { Sku = i.Sku, Quantidade = i.Quantidade, PrecoCentavos = Centavos(i.PrecoUnitario) })],
            Metadados = new MetadadosEntidade { Canal = metadados.Canal, Cupom = metadados.Cupom, Tags = [.. metadados.Tags] },
        });
        await db.SaveChangesAsync(ct);
    }

    public Task<PedidoDetalhe?> ObterDetalheAsync(PedidoId id, CancellationToken ct) => DetalheCompilado(db, id.Valor, ct);

    public async Task<Pedido?> ObterDominioAsync(PedidoId id, CancellationToken ct)
    {
        var e = await db.Pedidos.AsNoTracking().Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == id.Valor, ct);
        if (e is null) return null;
        return new Pedido(id, e.Cliente, Enum.Parse<Regiao>(e.Regiao), Enum.Parse<Prioridade>(e.Prioridade),
            [.. e.Itens.Select(i => new ItemPedido(i.Sku, i.Quantidade, Reais(i.PrecoCentavos)))]);
    }

    /// <summary>Consulta COM tracking: a entidade é alterada pelo mesmo DbContext e o EF gera um UPDATE só das colunas mudadas.</summary>
    public async Task<int?> AtualizarStatusAsync(PedidoId id, StatusPedido novoStatus, int? versaoEsperada, CancellationToken ct)
    {
        var e = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id.Valor, ct);
        if (e is null) return null;
        if (versaoEsperada is { } v && v != e.Versao) throw new ConflitoVersaoException(e.Id, e.Versao);

        e.Status = novoStatus.ToString();
        e.Versao++;
        e.AtualizadoEmUtc = relogio.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);   // Versao é token de concorrência: WHERE Id = @id AND Versao = @original
        return e.Versao;
    }

    /// <summary>Painel: projeção direta para DTO, sem tracking, ordenação determinística e contagem total.</summary>
    public async Task<PaginaDto<PedidoResumoDto>> ListarAsync(FiltroPainel filtro, CancellationToken ct)
    {
        var tamanho = Math.Clamp(filtro.Tamanho, 1, 100);
        var pagina = Math.Max(filtro.Pagina, 1);
        var consulta = db.Pedidos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtro.Status)) consulta = consulta.Where(p => p.Status == filtro.Status);

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderByDescending(p => p.CriadoEmUtc).ThenBy(p => p.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(p => new PedidoResumoDto(p.Id, p.Cliente, p.Status, p.TotalCentavos / 100m, p.Itens.Count, p.CriadoEmUtc))
            .ToListAsync(ct);
        return new PaginaDto<PedidoResumoDto>(itens, pagina, tamanho, total);
    }

    /// <summary>Versão ingênua usada como "antes" no diagnóstico: entidades completas com tracking e N+1 nos itens.</summary>
    public async Task<PaginaDto<PedidoResumoDto>> ListarIngenuoAsync(FiltroPainel filtro, CancellationToken ct)
    {
        var tamanho = Math.Clamp(filtro.Tamanho, 1, 100);
        var pagina = Math.Max(filtro.Pagina, 1);
        var todos = await db.Pedidos.ToListAsync(ct);   // traz tudo e filtra no cliente
        var filtrados = todos.Where(p => string.IsNullOrWhiteSpace(filtro.Status) || p.Status == filtro.Status)
            .OrderByDescending(p => p.CriadoEmUtc).ThenBy(p => p.Id).ToList();
        var resultado = new List<PedidoResumoDto>();
        foreach (var p in filtrados.Skip((pagina - 1) * tamanho).Take(tamanho))
        {
            await db.Entry(p).Collection(x => x.Itens).LoadAsync(ct);   // um comando por pedido (N+1)
            resultado.Add(new PedidoResumoDto(p.Id, p.Cliente, p.Status, Reais(p.TotalCentavos), p.Itens.Count, p.CriadoEmUtc));
        }
        return new PaginaDto<PedidoResumoDto>(resultado, pagina, tamanho, filtrados.Count);
    }

    public async Task<IReadOnlyList<EstatisticaStatusDto>> EstatisticasAsync(CancellationToken ct) =>
        await db.Pedidos.AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new EstatisticaStatusDto(g.Key, g.Count(), g.Sum(p => p.TotalCentavos) / 100m))
            .OrderBy(e => e.Status)
            .ToListAsync(ct);
}
