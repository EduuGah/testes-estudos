using System.Collections.Immutable;

namespace HubPedidos.Domain;

public enum Regiao { Sudeste, Sul, CentroOeste, Nordeste, Norte, Exterior }

public enum Prioridade { Normal, Alta }

public enum StatusPedido { Recebido, EmProcessamento, Processado, Falhou, Cancelado }

/// <summary>Item imutável: as invariantes são verificadas na construção.</summary>
public sealed record ItemPedido
{
    public ItemPedido(string sku, int quantidade, decimal precoUnitario)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantidade);
        ArgumentOutOfRangeException.ThrowIfNegative(precoUnitario);
        Sku = sku;
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
    }

    public string Sku { get; }
    public int Quantidade { get; init; }
    public decimal PrecoUnitario { get; init; }
    public decimal Subtotal => Quantidade * PrecoUnitario;
}

/// <summary>
/// Agregado imutável. ImmutableArray evita que alguém altere os itens depois da validação
/// (um List dentro de um record seria imutabilidade apenas superficial).
/// </summary>
public sealed record Pedido
{
    public Pedido(PedidoId id, string cliente, Regiao regiao, Prioridade prioridade, ImmutableArray<ItemPedido> itens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cliente);
        if (id.Valor == Guid.Empty) throw new ArgumentException("Identificador vazio.", nameof(id));
        Id = id;
        Cliente = cliente;
        Regiao = regiao;
        Prioridade = prioridade;
        Itens = itens.IsDefault ? [] : itens;
    }

    public PedidoId Id { get; }
    public string Cliente { get; }
    public Regiao Regiao { get; init; }
    public Prioridade Prioridade { get; init; }
    public ImmutableArray<ItemPedido> Itens { get; init; }
    public decimal Total => Itens.Sum(i => i.Subtotal);
    public int QuantidadeTotal => Itens.Sum(i => i.Quantidade);
}
