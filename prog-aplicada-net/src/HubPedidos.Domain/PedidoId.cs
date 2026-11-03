using System.Diagnostics.CodeAnalysis;

namespace HubPedidos.Domain;

/// <summary>Value object do identificador. IParsable permite usar o tipo direto em rotas e conversões.</summary>
public readonly record struct PedidoId(Guid Valor) : IParsable<PedidoId>
{
    public static PedidoId Novo() => new(Guid.CreateVersion7());

    public static PedidoId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var id) ? id : throw new FormatException($"Identificador de pedido inválido: '{s}'.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out PedidoId resultado)
    {
        if (Guid.TryParse(s, out var g) && g != Guid.Empty)
        {
            resultado = new PedidoId(g);
            return true;
        }
        resultado = default;
        return false;
    }

    public override string ToString() => Valor.ToString();
}
