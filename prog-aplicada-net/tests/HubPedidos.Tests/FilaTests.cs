using System.Threading.Channels;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Fila;
using HubPedidos.Application.Processamento;
using HubPedidos.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HubPedidos.Tests;

public class FilaTests
{
    private static FilaProcessamento Fila(int capacidade, BoundedChannelFullMode modo, out MetricasFila metricas)
    {
        metricas = new MetricasFila();
        return new FilaProcessamento(Options.Create(new OpcoesFila { Capacidade = capacidade, ModoCheio = modo }), metricas);
    }

    private static ProcessarPedidoJob Job() => new(PedidoId.Novo(), DateTimeOffset.UtcNow);

    [Fact]
    public async Task Modo_Wait_rejeita_no_TryWrite_e_faz_o_produtor_esperar_no_WriteAsync()
    {
        var fila = Fila(2, BoundedChannelFullMode.Wait, out var m);
        Assert.True(fila.TryEnfileirar(Job()));
        Assert.True(fila.TryEnfileirar(Job()));
        Assert.False(fila.TryEnfileirar(Job()));   // cheia: rejeição explícita

        var espera = fila.EnfileirarAsync(Job(), default).AsTask();
        Assert.False(espera.IsCompleted);             // backpressure: o produtor aguarda
        Assert.True(fila.Leitor.TryRead(out _));      // consumidor libera espaço
        await espera.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, m.Snapshot(fila.Profundidade).Rejeitados);
    }

    [Fact]
    public void Modo_DropOldest_descarta_e_contabiliza()
    {
        var fila = Fila(3, BoundedChannelFullMode.DropOldest, out var m);
        for (var i = 0; i < 10; i++) Assert.True(fila.TryEnfileirar(Job()));
        var s = m.Snapshot(fila.Profundidade);
        Assert.Equal(7, s.Descartados);
        Assert.Equal(0, s.Rejeitados);
        Assert.Equal(3, s.Profundidade);
    }

    [Fact]
    public async Task Produtor_cancelado_nao_fica_preso()
    {
        var fila = Fila(1, BoundedChannelFullMode.Wait, out _);
        fila.TryEnfileirar(Job());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fila.EnfileirarAsync(Job(), cts.Token).AsTask());
    }

    [Fact]
    public void Fila_concluida_nao_aceita_novos_itens()
    {
        var fila = Fila(5, BoundedChannelFullMode.Wait, out _);
        fila.Concluir();
        Assert.False(fila.TryEnfileirar(Job()));
    }

    [Fact]
    public async Task Semaforo_nunca_excede_o_limite_e_nao_perde_slots_com_excecao()
    {
        var limitador = new LimitadorBanco(Options.Create(new OpcoesFila { MaxOperacoesBanco = 3 }));
        var tarefas = Enumerable.Range(0, 60).Select(async i =>
        {
            try
            {
                await limitador.ExecutarAsync<int>(async ct =>
                {
                    await Task.Delay(5, ct);
                    if (i % 4 == 0) throw new InvalidOperationException("falha simulada");
                    return i;
                }, default);
            }
            catch (InvalidOperationException) { }
        });
        await Task.WhenAll(tarefas);
        Assert.True(limitador.MaximoObservado <= 3);
        Assert.Equal(3, limitador.SlotsLivres);   // todos os slots voltaram
    }

    [Fact]
    public async Task Falha_de_um_item_nao_derruba_o_worker_e_nenhum_pedido_e_processado_duas_vezes()
    {
        var falha = PedidoId.Novo();
        var repo = new RepositorioFalso(falha);
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.Configure<OpcoesFila>(o => { o.Capacidade = 50; o.Consumidores = 3; o.MaxOperacoesBanco = 2; });
        servicos.AddSingleton<MetricasFila>().AddSingleton<FilaProcessamento>().AddSingleton<LimitadorBanco>();
        var catalogo = CatalogoProcessadores.Construir(typeof(CatalogoProcessadores).Assembly, "HubPedidos.Application.Processamento");
        servicos.AddSingleton(catalogo);
        foreach (var t in catalogo.Itens.Values.Distinct()) servicos.AddScoped(t);
        servicos.AddScoped<SeletorProcessador>().AddScoped<ServicoProcessamento>();
        servicos.AddSingleton<IPedidoRepositorio>(repo);
        servicos.AddSingleton<INotificadorPedidos, NotificadorNulo>();
        servicos.AddSingleton<WorkerProcessamento>();
        await using var sp = servicos.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var fila = sp.GetRequiredService<FilaProcessamento>();
        var worker = sp.GetRequiredService<WorkerProcessamento>();
        await worker.StartAsync(default);

        var ids = Enumerable.Range(0, 9).Select(_ => PedidoId.Novo()).Append(falha).ToList();
        foreach (var id in ids) repo.Registrar(id);
        foreach (var id in ids) Assert.True(fila.TryEnfileirar(new ProcessarPedidoJob(id, DateTimeOffset.UtcNow)));
        Assert.True(fila.TryEnfileirar(new ProcessarPedidoJob(ids[0], DateTimeOffset.UtcNow)));   // duplicado proposital

        await worker.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);   // conclui e drena
        var s = sp.GetRequiredService<MetricasFila>().Snapshot(fila.Profundidade);
        Assert.Equal(9, s.Processados);
        Assert.Equal(1, s.Falhas);
        Assert.Equal(1, s.Duplicados);
        Assert.Equal(0, s.Profundidade);
        Assert.True(sp.GetRequiredService<LimitadorBanco>().MaximoObservado <= 2);
        Assert.All(ids.Take(9), id => Assert.Equal(1, repo.VezesProcessado(id)));
    }

    private sealed class NotificadorNulo : INotificadorPedidos
    {
        public Task PedidoAtualizadoAsync(PedidoAtualizadoEvento evento, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class RepositorioFalso(PedidoId falha) : IPedidoRepositorio
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<PedidoId, int> _processado = new();
        public void Registrar(PedidoId id) => _processado[id] = 0;
        public int VezesProcessado(PedidoId id) => _processado[id];

        public Task<Pedido?> ObterDominioAsync(PedidoId id, CancellationToken ct) =>
            id == falha
                ? throw new InvalidOperationException("banco indisponível para este pedido")
                : Task.FromResult<Pedido?>(new Pedido(id, "ana", Regiao.Sul, Prioridade.Alta, [new ItemPedido("X", 1, 10m)]));

        public Task<int?> AtualizarStatusAsync(PedidoId id, StatusPedido novoStatus, int? versaoEsperada, CancellationToken ct)
        {
            if (novoStatus == StatusPedido.Processado) _processado.AddOrUpdate(id, 1, (_, v) => v + 1);
            return Task.FromResult<int?>(1);
        }

        public Task AdicionarAsync(Pedido pedido, MetadadosPedido metadados, CancellationToken ct) => Task.CompletedTask;
        public Task<PedidoDetalhe?> ObterDetalheAsync(PedidoId id, CancellationToken ct) => Task.FromResult<PedidoDetalhe?>(null);
        public Task<PaginaDto<PedidoResumoDto>> ListarAsync(FiltroPainel filtro, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<EstatisticaStatusDto>> EstatisticasAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
