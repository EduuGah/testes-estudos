using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using HubPedidos.Application.Contratos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace HubPedidos.Ferramentas;

/// <summary>
/// Cenário de tempo real: autorização no hub, evento tipado, streaming,
/// queda forçada da conexão, reconexão automática e reconciliação via HTTP.
/// </summary>
public static class TempoReal
{
    private static void Log(string quem, string texto) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{quem,-5}] {texto}");

    public static async Task<int> ExecutarAsync(string url)
    {
        using var anaHttp = Http(url, "ana");
        var resp = await anaHttp.PostAsJsonAsync("/api/v1/pedidos", new CriarPedidoRequest("Sul", "Normal", [new ItemRequest("CAFE-01", 2, 19.90m)]));
        var pedido = (await resp.Content.ReadFromJsonAsync<PedidoCriadoResponse>())!;
        Log("http", $"POST /api/v1/pedidos -> {(int)resp.StatusCode} pedido {pedido.Id} ({pedido.Categoria})");

        // proxy TCP de falha controlada: a conexão SignalR da Ana passa por ele; "Derrubar" corta o socket
        // de forma abrupta (como uma queda de rede), o que dispara a reconexão automática do cliente
        var destino = new Uri(url);
        await using var proxy = new ProxyFalha(5090, destino.Port);
        var ana = Conexao("http://127.0.0.1:5090", "ana");
        var bruno = Conexao(url, "bruno");
        var reconciliado = new TaskCompletionSource();

        ana.On<PedidoAtualizadoEvento>("PedidoAtualizado", e => Log("ana", $"evento PedidoAtualizado: {e.Status} (v{e.Versao})"));
        bruno.On<PedidoAtualizadoEvento>("PedidoAtualizado", e => Log("bruno", $"evento indevido: {e.Status}"));
        ana.Reconnecting += ex => { Log("ana", $"estado: Reconectando (ações desabilitadas) – {ex?.GetType().Name}"); return Task.CompletedTask; };
        ana.Reconnected += async id =>
        {
            Log("ana", $"estado: Conectado novamente (nova conexão {id?[..8]}…)");
            await ana.InvokeAsync("AcompanharPedido", pedido.Id);   // o servidor revalida a autorização
            Log("ana", "reingressou no grupo do pedido após revalidação");
            var atual = await Detalhe(anaHttp, pedido.Id);           // reconciliação: eventos perdidos durante a queda
            Log("ana", $"reconciliação HTTP: status atual = {atual.Status}, versão = {atual.Versao}");
            reconciliado.TrySetResult();
        };
        ana.Closed += ex => { Log("ana", "estado: Desconectado"); return Task.CompletedTask; };

        await ana.StartAsync();
        await bruno.StartAsync();
        Log("ana", $"estado: {ana.State}");

        await ana.InvokeAsync("AcompanharPedido", pedido.Id);
        Log("ana", "AcompanharPedido autorizado: entrou no grupo do pedido");
        try { await bruno.InvokeAsync("AcompanharPedido", pedido.Id); }
        catch (HubException ex) { Log("bruno", $"AcompanharPedido negado pelo servidor: {ex.Message.Split(':').Last().Trim()}"); }

        // streaming de status de um novo pedido
        var r2 = await anaHttp.PostAsJsonAsync("/api/v1/pedidos", new CriarPedidoRequest("Norte", "Normal", [new ItemRequest("CAIXA", 150, 2m)]));
        var p2 = (await r2.Content.ReadFromJsonAsync<PedidoCriadoResponse>())!;
        await foreach (var s in ana.StreamAsync<string>("StreamStatus", p2.Id))
            Log("ana", $"stream do pedido {p2.Id.ToString()[..8]}…: {s}");

        var atual = await Detalhe(anaHttp, pedido.Id);
        Log("ana", $"estado inicial via HTTP: {atual.Status}");
        await AlterarAsync(anaHttp, pedido.Id, "Cancelado");     // gera evento com a conexão ativa
        await Task.Delay(300);

        // queda de rede simulada no proxy (o servidor continua de pé)
        Log("proxy", $"queda de rede simulada: {proxy.Derrubar()} conexão(ões) TCP cortada(s)");
        await Task.Delay(200);
        await AlterarAsync(anaHttp, pedido.Id, "Recebido");      // mudança DURANTE a queda: o evento se perde
        await reconciliado.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await AlterarAsync(anaHttp, pedido.Id, "Cancelado");     // após reconectar, eventos voltam a chegar
        await Task.Delay(300);
        await bruno.DisposeAsync();
        await ana.DisposeAsync();
        return 0;
    }

    private static HttpClient Http(string url, string? usuario)
    {
        var h = new HttpClient { BaseAddress = new Uri(url) };
        if (usuario is not null) h.DefaultRequestHeaders.Add("X-Usuario", usuario);
        return h;
    }

    private static HubConnection Conexao(string url, string usuario) =>
        new HubConnectionBuilder()
            .WithUrl($"{url}/hubs/pedidos?usuario={usuario}")
            .WithAutomaticReconnect([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)])   // backoff
            .Build();

    private sealed record DetalheV1(Guid Id, string Status, decimal Total);
    private sealed record Versionado(string Status, int Versao);

    private static async Task<Versionado> Detalhe(HttpClient http, Guid id)
    {
        var v2 = await http.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v2/pedidos/{id}");
        return new Versionado(v2.GetProperty("situacao").GetProperty("codigo").GetString()!, v2.GetProperty("versao").GetInt32());
    }

    private static async Task AlterarAsync(HttpClient http, Guid id, string status)
    {
        var get = await http.GetAsync($"/api/v1/pedidos/{id}");
        var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/pedidos/{id}/status") { Content = JsonContent.Create(new { status }) };
        put.Headers.IfMatch.Add(get.Headers.ETag!);
        var r = await http.SendAsync(put);
        Log("http", $"PUT status={status} (If-Match {get.Headers.ETag}) -> {(int)r.StatusCode}");
    }
}

/// <summary>Proxy TCP mínimo que permite cortar todas as conexões ativas para simular perda de rede.</summary>
public sealed class ProxyFalha : IAsyncDisposable
{
    private readonly TcpListener _ouvinte;
    private readonly int _destino;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<TcpClient, TcpClient> _pares = new();

    public ProxyFalha(int porta, int destino)
    {
        _destino = destino;
        _ouvinte = new TcpListener(IPAddress.Loopback, porta);
        _ouvinte.Start();
        _ = AceitarAsync();
    }

    private async Task AceitarAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var cliente = await _ouvinte.AcceptTcpClientAsync(_cts.Token);
                var servidor = new TcpClient();
                await servidor.ConnectAsync(IPAddress.Loopback, _destino, _cts.Token);
                _pares[cliente] = servidor;
                _ = CopiarAsync(cliente.GetStream(), servidor.GetStream());
                _ = CopiarAsync(servidor.GetStream(), cliente.GetStream());
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task CopiarAsync(Stream origem, Stream destino)
    {
        try { await origem.CopyToAsync(destino); }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    public int Derrubar()
    {
        var n = 0;
        foreach (var (cliente, servidor) in _pares)
        {
            cliente.Client.LingerState = new LingerOption(true, 0);   // fecha com RST: queda abrupta
            cliente.Close();
            servidor.Close();
            _pares.TryRemove(cliente, out _);
            n++;
        }
        return n;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        Derrubar();
        _ouvinte.Stop();
        _cts.Dispose();
    }
}
