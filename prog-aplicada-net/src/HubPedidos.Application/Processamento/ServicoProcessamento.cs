using System.Diagnostics;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Fila;
using HubPedidos.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HubPedidos.Application.Processamento;

/// <summary>Seleciona a estratégia pelo catálogo e resolve a instância no escopo atual (registro feito no composition root).</summary>
public sealed class SeletorProcessador(CatalogoProcessadores catalogo, IServiceProvider escopo)
{
    public IProcessadorPedido Obter(CategoriaProcessamento categoria) =>
        (IProcessadorPedido)escopo.GetRequiredService(catalogo.TipoPara(categoria));
}

public sealed class ServicoProcessamento(
    IPedidoRepositorio repositorio,
    SeletorProcessador seletor,
    LimitadorBanco limitador,
    INotificadorPedidos notificador,
    ILogger<ServicoProcessamento> logger)
{
    public async Task<bool> ProcessarAsync(ProcessarPedidoJob job, CancellationToken ct)
    {
        var id = job.PedidoId;
        await limitador.ExecutarAsync(c => repositorio.AtualizarStatusAsync(id, StatusPedido.EmProcessamento, null, c), ct);
        var pedido = await limitador.ExecutarAsync(c => repositorio.ObterDominioAsync(id, c), ct);

        var categoria = ClassificadorPedidos.Classificar(pedido);
        StatusPedido final;
        if (categoria == CategoriaProcessamento.Invalido || pedido is null)
        {
            final = StatusPedido.Falhou;
        }
        else
        {
            // o trabalho da estratégia acontece FORA do semáforo: o slot do banco não fica preso à toa
            var resultado = await seletor.Obter(categoria).ProcessarAsync(pedido, ct);
            logger.LogInformation("Pedido {PedidoId} processado por {Processador} ({Categoria})", id, resultado.Processador, categoria);
            final = StatusPedido.Processado;
        }

        var versao = await limitador.ExecutarAsync(c => repositorio.AtualizarStatusAsync(id, final, null, c), ct);
        // o evento só é publicado depois da mudança persistida
        await notificador.PedidoAtualizadoAsync(new PedidoAtualizadoEvento(id.Valor, final.ToString(), versao ?? 0, DateTimeOffset.UtcNow), ct);
        return final == StatusPedido.Processado;
    }
}

/// <summary>Consumidores do canal: um escopo de DI por item, falhas registradas sem derrubar o worker.</summary>
public sealed class WorkerProcessamento(
    FilaProcessamento fila,
    MetricasFila metricas,
    IServiceScopeFactory escopos,
    Microsoft.Extensions.Options.IOptions<OpcoesFila> opcoes,
    ILogger<WorkerProcessamento> logger) : Microsoft.Extensions.Hosting.BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(1, opcoes.Value.Consumidores).Select(n => ConsumirAsync(n, stoppingToken)));

    private async Task ConsumirAsync(int numero, CancellationToken stoppingToken)
    {
        // a leitura NÃO usa o token de parada: o laço termina quando o canal é concluído e esvaziado (drenagem)
        await foreach (var job in fila.Leitor.ReadAllAsync(CancellationToken.None))
        {
            if (!metricas.IniciarProcessamento(job.PedidoId)) continue;
            var cronometro = Stopwatch.StartNew();
            var sucesso = false;
            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                var servico = escopo.ServiceProvider.GetRequiredService<ServicoProcessamento>();
                sucesso = await servico.ProcessarAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Consumidor {Numero}: parada solicitada durante o pedido {PedidoId}", numero, job.PedidoId);
                throw;
            }
            catch (Exception ex)
            {
                // a falha de um item não encerra o consumidor
                logger.LogError(ex, "Consumidor {Numero}: falha no pedido {PedidoId}", numero, job.PedidoId);
            }
            finally
            {
                var espera = (DateTimeOffset.UtcNow - job.EnfileiradoEm).TotalMilliseconds;
                metricas.Concluido(job.PedidoId, sucesso, espera);
                logger.LogDebug("Pedido {PedidoId} concluído em {Ms} ms (sucesso={Sucesso})", job.PedidoId, cronometro.ElapsedMilliseconds, sucesso);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        fila.Concluir();   // 1) para de aceitar novos itens
        logger.LogInformation("Encerrando: {Pendentes} item(ns) na fila serão drenados até o prazo do host", fila.Profundidade);
        // 2) espera a drenagem até o prazo do host; 3) depois disso, cancela o que ainda estiver em andamento
        // no .NET 10 o ExecuteAsync roda em outra thread: aguarda-se o ExecuteTask, não uma variável preenchida por ele
        await Task.WhenAny(ExecuteTask ?? Task.CompletedTask, Task.Delay(Timeout.Infinite, cancellationToken));
        await base.StopAsync(cancellationToken);
    }
}
