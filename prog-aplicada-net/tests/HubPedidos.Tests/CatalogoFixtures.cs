using HubPedidos.Application.Processamento;
using HubPedidos.Domain;

namespace HubPedidos.Tests.CatalogoDuplicado;

[ProcessadorPedido(CategoriaProcessamento.Padrao)]
public sealed class PrimeiroPadrao : IProcessadorPedido
{
    public Task<ResultadoProcessamento> ProcessarAsync(Pedido pedido, CancellationToken ct) => Task.FromResult(new ResultadoProcessamento("1", ""));
}

[ProcessadorPedido(CategoriaProcessamento.Padrao)]
public sealed class SegundoPadrao : IProcessadorPedido
{
    public Task<ResultadoProcessamento> ProcessarAsync(Pedido pedido, CancellationToken ct) => Task.FromResult(new ResultadoProcessamento("2", ""));
}
