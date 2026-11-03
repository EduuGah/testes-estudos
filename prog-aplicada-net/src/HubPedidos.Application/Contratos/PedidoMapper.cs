using System.Collections.Immutable;
using HubPedidos.Domain;

namespace HubPedidos.Application.Contratos;

/// <summary>Fronteira explícita DTO → domínio: tudo que pode falhar é verificado antes de criar o objeto de domínio.</summary>
public static class PedidoMapper
{
    public const int MaximoItens = 200;

    public static bool TryCriar(CriarPedidoRequest? request, string cliente, out Pedido? pedido, out IReadOnlyDictionary<string, string[]> erros)
    {
        var e = new Dictionary<string, string[]>();
        pedido = null;

        if (request is null)
        {
            e["corpo"] = ["O corpo da requisição é obrigatório."];
            erros = e;
            return false;
        }

        if (!Enum.TryParse<Regiao>(request.Regiao, ignoreCase: true, out var regiao) || !Enum.IsDefined(regiao))
            e["regiao"] = [$"Região inválida: '{request.Regiao}'."];

        var prioridade = Prioridade.Normal;
        if (request.Prioridade is not null && (!Enum.TryParse(request.Prioridade, ignoreCase: true, out prioridade) || !Enum.IsDefined(prioridade)))
            e["prioridade"] = [$"Prioridade inválida: '{request.Prioridade}'."];

        var itens = request.Itens ?? [];
        if (itens.Count is 0 or > MaximoItens)
            e["itens"] = [$"Informe de 1 a {MaximoItens} itens."];

        for (var i = 0; i < itens.Count; i++)
        {
            var item = itens[i];
            if (string.IsNullOrWhiteSpace(item.Sku)) e[$"itens[{i}].sku"] = ["SKU obrigatório."];
            if (item.Quantidade <= 0) e[$"itens[{i}].quantidade"] = ["A quantidade deve ser maior que zero."];
            if (item.PrecoUnitario < 0) e[$"itens[{i}].precoUnitario"] = ["O preço não pode ser negativo."];
        }

        erros = e;
        if (e.Count > 0) return false;

        pedido = new Pedido(
            PedidoId.Novo(), cliente, regiao, prioridade,
            [.. itens.Select(i => new ItemPedido(i.Sku!.Trim(), i.Quantidade, i.PrecoUnitario))]);
        return true;
    }

    public static MetadadosPedido Metadados(CriarPedidoRequest request) =>
        new(string.IsNullOrWhiteSpace(request.Canal) ? "api" : request.Canal.Trim().ToLowerInvariant(), request.Cupom, []);
}
