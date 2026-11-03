using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HubPedidos.Application.Contratos;
using HubPedidos.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.SignalR;

namespace HubPedidos.Api.TempoReal;

/// <summary>Métodos que o servidor chama no cliente (contrato tipado).</summary>
public interface IPedidosCliente
{
    Task PedidoAtualizado(PedidoAtualizadoEvento evento);
}

[Authorize]
public sealed class PedidosHub(IPedidoRepositorio repositorio, RegistroConexoes registro) : Hub<IPedidosCliente>
{
    public static string Grupo(Guid pedidoId) => $"pedido:{pedidoId}";

    public override Task OnConnectedAsync()
    {
        registro.Adicionar(Context);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registro.Remover(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>A autorização é verificada AQUI, no servidor, antes de entrar no grupo; o grupo não é mecanismo de segurança.</summary>
    public async Task AcompanharPedido(Guid pedidoId)
    {
        await AutorizarAsync(pedidoId, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(pedidoId));
    }

    public Task DeixarPedido(Guid pedidoId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(pedidoId));

    /// <summary>Streaming: envia cada mudança de status até o pedido terminar ou o cliente cancelar.</summary>
    public async IAsyncEnumerable<string> StreamStatus(Guid pedidoId, [EnumeratorCancellation] CancellationToken ct)
    {
        await AutorizarAsync(pedidoId, ct);
        string? ultimo = null;
        while (!ct.IsCancellationRequested)
        {
            var d = await repositorio.ObterDetalheAsync(new PedidoId(pedidoId), ct);
            if (d is not null && d.Status != ultimo)
            {
                ultimo = d.Status;
                yield return $"{d.Status} (v{d.Versao})";
            }
            if (ultimo is "Processado" or "Falhou" or "Cancelado") yield break;
            await Task.Delay(50, ct);
        }
    }

    private async Task AutorizarAsync(Guid pedidoId, CancellationToken ct)
    {
        var d = await repositorio.ObterDetalheAsync(new PedidoId(pedidoId), ct);
        if (d is null || d.Cliente != Context.UserIdentifier)
            throw new HubException("Acesso negado: o pedido não pertence ao usuário conectado.");
    }
}

/// <summary>Conexões ativas (usado só pelo endpoint de laboratório que derruba conexões para testar a reconexão).</summary>
public sealed class RegistroConexoes
{
    private readonly ConcurrentDictionary<string, HubCallerContext> _conexoes = new();
    public void Adicionar(HubCallerContext c) => _conexoes[c.ConnectionId] = c;
    public void Remover(string id) => _conexoes.TryRemove(id, out _);
    public int AbortarTodas()
    {
        var n = 0;
        foreach (var c in _conexoes.Values) { c.Abort(); n++; }
        return n;
    }
}

/// <summary>Módulo de notificações: invalida o cache de leitura e avisa os clientes autorizados.</summary>
public sealed class NotificadorApi(IHubContext<PedidosHub, IPedidosCliente> hub, IOutputCacheStore cache) : INotificadorPedidos
{
    public const string TagCache = "pedidos";

    public async Task PedidoAtualizadoAsync(PedidoAtualizadoEvento evento, CancellationToken ct)
    {
        await cache.EvictByTagAsync(TagCache, ct);
        await hub.Clients.Group(PedidosHub.Grupo(evento.PedidoId)).PedidoAtualizado(evento);
    }
}
