namespace HubPedidos.Application.Contratos;

// DTOs de entrada: aceitam texto "cru" vindo do cliente; a validação acontece no mapeador.
public sealed record ItemRequest(string? Sku, int Quantidade, decimal PrecoUnitario);

public sealed record CriarPedidoRequest(
    string? Regiao,
    string? Prioridade,
    IReadOnlyList<ItemRequest>? Itens,
    string? Canal = null,
    string? Cupom = null);

// DTOs de saída: nunca expõem o tipo de domínio.
public sealed record PedidoCriadoResponse(Guid Id, string Categoria, string Status, decimal Total);

public sealed record PedidoResumoDto(Guid Id, string Cliente, string Status, decimal Total, int QuantidadeItens, DateTime CriadoEmUtc);

public sealed record PaginaDto<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int Total);

public sealed record ItemDetalhe(string Sku, int Quantidade, decimal PrecoUnitario);

/// <summary>Representação interna lida do banco; as versões v1 e v2 da API são montadas a partir dela.</summary>
public sealed record PedidoDetalhe(
    Guid Id, string Cliente, string Regiao, string Prioridade, string Status, decimal Total,
    int Versao, DateTime AtualizadoEmUtc, IReadOnlyList<ItemDetalhe> Itens, string Canal);

public sealed record EstatisticaStatusDto(string Status, int Quantidade, decimal Valor);

public sealed record MetadadosPedido(string Canal, string? Cupom, IReadOnlyList<string> Tags);

public sealed record FiltroPainel(string? Status, int Pagina = 1, int Tamanho = 20);

public sealed record PedidoAtualizadoEvento(Guid PedidoId, string Status, int Versao, DateTimeOffset Instante);
