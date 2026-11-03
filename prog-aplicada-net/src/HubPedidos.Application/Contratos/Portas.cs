using HubPedidos.Domain;

namespace HubPedidos.Application.Contratos;

/// <summary>Porta de persistência: definida aqui (quem consome) e implementada na infraestrutura.</summary>
public interface IPedidoRepositorio
{
    Task AdicionarAsync(Pedido pedido, MetadadosPedido metadados, CancellationToken ct);
    Task<PedidoDetalhe?> ObterDetalheAsync(PedidoId id, CancellationToken ct);
    Task<Pedido?> ObterDominioAsync(PedidoId id, CancellationToken ct);

    /// <summary>Retorna a nova versão, ou null se o pedido não existe. Lança ConflitoVersaoException se a versão esperada estiver obsoleta.</summary>
    Task<int?> AtualizarStatusAsync(PedidoId id, StatusPedido novoStatus, int? versaoEsperada, CancellationToken ct);

    Task<PaginaDto<PedidoResumoDto>> ListarAsync(FiltroPainel filtro, CancellationToken ct);
    Task<IReadOnlyList<EstatisticaStatusDto>> EstatisticasAsync(CancellationToken ct);
}

/// <summary>Porta de notificação: o módulo de notificações (SignalR + cache) a implementa.</summary>
public interface INotificadorPedidos
{
    Task PedidoAtualizadoAsync(PedidoAtualizadoEvento evento, CancellationToken ct);
}

public sealed class ConflitoVersaoException(Guid pedidoId, int versaoAtual)
    : Exception($"O pedido {pedidoId} está na versão {versaoAtual}.")
{
    public int VersaoAtual { get; } = versaoAtual;
}
