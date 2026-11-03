using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using HubPedidos.Domain;
using Microsoft.Extensions.Options;

namespace HubPedidos.Application.Fila;

/// <summary>Job imutável e mínimo: só o identificador. Nada de DbContext ou entidade dentro da fila.</summary>
public sealed record ProcessarPedidoJob(PedidoId PedidoId, DateTimeOffset EnfileiradoEm);

public sealed class OpcoesFila
{
    public int Capacidade { get; set; } = 100;
    public int Consumidores { get; set; } = 4;
    public int MaxOperacoesBanco { get; set; } = 4;
    public BoundedChannelFullMode ModoCheio { get; set; } = BoundedChannelFullMode.Wait;
}

/// <summary>Contadores atômicos e estado por pedido (ConcurrentDictionary com operações atômicas).</summary>
public sealed class MetricasFila : IDisposable
{
    public const string NomeMeter = "HubPedidos.Fila";
    private readonly Meter _meter = new(NomeMeter, "1.0");
    private readonly ConcurrentDictionary<PedidoId, StatusPedido> _estados = new();
    private readonly ConcurrentQueue<double> _latenciasMs = new();
    private long _aceitos, _rejeitados, _descartados, _processados, _falhas, _duplicados, _profundidadeMax;

    public MetricasFila()
    {
        _meter.CreateObservableCounter("fila.aceitos", () => Interlocked.Read(ref _aceitos));
        _meter.CreateObservableCounter("fila.rejeitados", () => Interlocked.Read(ref _rejeitados));
        _meter.CreateObservableCounter("fila.descartados", () => Interlocked.Read(ref _descartados));
        _meter.CreateObservableCounter("fila.processados", () => Interlocked.Read(ref _processados));
        _meter.CreateObservableGauge("fila.profundidade", () => ProfundidadeAtual?.Invoke() ?? 0);
    }

    public Func<int>? ProfundidadeAtual { get; set; }

    public void Aceito(int profundidade)
    {
        Interlocked.Increment(ref _aceitos);
        long atual;
        while (profundidade > (atual = Interlocked.Read(ref _profundidadeMax)))
            if (Interlocked.CompareExchange(ref _profundidadeMax, profundidade, atual) == atual) break;
    }

    public void Rejeitado() => Interlocked.Increment(ref _rejeitados);
    public void Descartado() => Interlocked.Increment(ref _descartados);

    /// <summary>TryAdd é atômico: se outro consumidor já pegou o pedido, devolve false (evita processar duas vezes).</summary>
    public bool IniciarProcessamento(PedidoId id)
    {
        if (_estados.TryAdd(id, StatusPedido.EmProcessamento)) return true;
        Interlocked.Increment(ref _duplicados);
        return false;
    }

    public void Concluido(PedidoId id, bool sucesso, double latenciaMs)
    {
        _estados[id] = sucesso ? StatusPedido.Processado : StatusPedido.Falhou;
        Interlocked.Increment(ref sucesso ? ref _processados : ref _falhas);
        _latenciasMs.Enqueue(latenciaMs);
    }

    public SnapshotFila Snapshot(int profundidade)
    {
        var lat = _latenciasMs.ToArray();
        Array.Sort(lat);
        return new SnapshotFila(
            Interlocked.Read(ref _aceitos), Interlocked.Read(ref _rejeitados), Interlocked.Read(ref _descartados),
            Interlocked.Read(ref _processados), Interlocked.Read(ref _falhas), Interlocked.Read(ref _duplicados),
            profundidade, Interlocked.Read(ref _profundidadeMax), Percentil(lat, 0.50), Percentil(lat, 0.95));
    }

    private static double Percentil(double[] ordenado, double p) =>
        ordenado.Length == 0 ? 0 : ordenado[Math.Clamp((int)Math.Ceiling(p * ordenado.Length) - 1, 0, ordenado.Length - 1)];

    public void Dispose() => _meter.Dispose();
}

public sealed record SnapshotFila(
    long Aceitos, long Rejeitados, long Descartados, long Processados, long Falhas, long Duplicados,
    int Profundidade, long ProfundidadeMaxima, double LatenciaP50Ms, double LatenciaP95Ms);

/// <summary>Canal limitado: a capacidade é o mecanismo de backpressure.</summary>
public sealed class FilaProcessamento
{
    private readonly Channel<ProcessarPedidoJob> _canal;
    private readonly MetricasFila _metricas;

    public FilaProcessamento(IOptions<OpcoesFila> opcoes, MetricasFila metricas)
    {
        var o = opcoes.Value;
        _metricas = metricas;
        Capacidade = o.Capacidade;
        ModoCheio = o.ModoCheio;
        // itemDropped só é chamado nos modos de descarte (DropNewest, DropOldest, DropWrite)
        _canal = Channel.CreateBounded<ProcessarPedidoJob>(
            new BoundedChannelOptions(o.Capacidade) { FullMode = o.ModoCheio, SingleReader = false, SingleWriter = false },
            _ => metricas.Descartado());
        metricas.ProfundidadeAtual = () => _canal.Reader.Count;
    }

    public int Capacidade { get; }
    public BoundedChannelFullMode ModoCheio { get; }
    public int Profundidade => _canal.Reader.Count;
    public ChannelReader<ProcessarPedidoJob> Leitor => _canal.Reader;

    /// <summary>Rejeição explícita: no modo Wait, TryWrite devolve false quando a fila está cheia (a API responde 503).</summary>
    public bool TryEnfileirar(ProcessarPedidoJob job)
    {
        if (_canal.Writer.TryWrite(job))
        {
            _metricas.Aceito(_canal.Reader.Count);
            return true;
        }
        _metricas.Rejeitado();
        return false;
    }

    /// <summary>Produtor que aceita esperar: WriteAsync aguarda espaço (backpressure) ou cancelamento.</summary>
    public async ValueTask EnfileirarAsync(ProcessarPedidoJob job, CancellationToken ct)
    {
        await _canal.Writer.WriteAsync(job, ct);
        _metricas.Aceito(_canal.Reader.Count);
    }

    /// <summary>Encerramento: não aceita novos itens; os consumidores drenam o que já está na fila.</summary>
    public void Concluir() => _canal.Writer.TryComplete();
}

/// <summary>Limita quantas operações chegam ao banco ao mesmo tempo, independentemente do número de consumidores.</summary>
public sealed class LimitadorBanco
{
    private readonly SemaphoreSlim _semaforo;
    private int _atual, _maximoObservado;

    public LimitadorBanco(IOptions<OpcoesFila> opcoes)
    {
        Limite = opcoes.Value.MaxOperacoesBanco;
        _semaforo = new SemaphoreSlim(Limite, Limite);
    }

    public int Limite { get; }
    public int MaximoObservado => Volatile.Read(ref _maximoObservado);
    public int SlotsLivres => _semaforo.CurrentCount;

    public async Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> operacao, CancellationToken ct)
    {
        await _semaforo.WaitAsync(ct);
        try
        {
            var atual = Interlocked.Increment(ref _atual);
            int max;
            while (atual > (max = Volatile.Read(ref _maximoObservado)))
                if (Interlocked.CompareExchange(ref _maximoObservado, atual, max) == max) break;
            return await operacao(ct);
        }
        finally
        {
            Interlocked.Decrement(ref _atual);
            _semaforo.Release();   // sempre devolve o slot, mesmo com exceção
        }
    }
}
