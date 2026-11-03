using System.Security.Cryptography;
using System.Text.Json;
using HubPedidos.Application.Contratos;
using Microsoft.Net.Http.Headers;

namespace HubPedidos.Api.Endpoints;

// ---- Contrato v1 (publicado, mantido sem alteração) ----
public sealed record ItemV1(string Sku, int Quantidade, decimal PrecoUnitario);
public sealed record PedidoV1(Guid Id, string Cliente, string Status, decimal Total, IReadOnlyList<ItemV1> Itens);

// ---- Contrato v2 (mudança incompatível deliberada: status e total viraram objetos) ----
public sealed record SituacaoV2(string Codigo, string Descricao, DateTime AtualizadoEmUtc);
public sealed record ValoresV2(decimal Total, string Moeda);
public sealed record ItemV2(string Sku, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
public sealed record PedidoV2(Guid Id, string Cliente, SituacaoV2 Situacao, ValoresV2 Valores, IReadOnlyList<ItemV2> Itens, int Versao, string Canal);

public sealed record AlterarStatusRequest(string? Status);

public static class Representacoes
{
    private static readonly Dictionary<string, string> Descricoes = new()
    {
        ["Recebido"] = "Pedido recebido e aguardando processamento",
        ["EmProcessamento"] = "Pedido em processamento",
        ["Processado"] = "Pedido processado",
        ["Falhou"] = "Falha no processamento",
        ["Cancelado"] = "Pedido cancelado",
    };

    public static PedidoV1 V1(PedidoDetalhe d) =>
        new(d.Id, d.Cliente, d.Status, d.Total, [.. d.Itens.Select(i => new ItemV1(i.Sku, i.Quantidade, i.PrecoUnitario))]);

    public static PedidoV2 V2(PedidoDetalhe d) =>
        new(d.Id, d.Cliente,
            new SituacaoV2(d.Status, Descricoes.GetValueOrDefault(d.Status, d.Status), d.AtualizadoEmUtc),
            new ValoresV2(d.Total, "BRL"),
            [.. d.Itens.Select(i => new ItemV2(i.Sku, i.Quantidade, i.PrecoUnitario, i.Quantidade * i.PrecoUnitario))],
            d.Versao, d.Canal);

    /// <summary>ETag forte calculado sobre a representação enviada: muda quando a representação muda.</summary>
    public static string Etag<T>(T representacao)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(representacao, JsonSerializerOptions.Web);
        return $"\"{Convert.ToHexStringLower(SHA256.HashData(bytes))[..20]}\"";
    }

    public static bool Corresponde(string? cabecalho, string etag)
    {
        if (string.IsNullOrWhiteSpace(cabecalho)) return false;
        if (!EntityTagHeaderValue.TryParseList([cabecalho], out var lista)) return false;
        return lista.Any(t => t.Equals(EntityTagHeaderValue.Any) || (!t.IsWeak && t.Tag.Value == etag));
    }
}
