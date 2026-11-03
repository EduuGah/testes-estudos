using HubPedidos.Domain;

namespace HubPedidos.Application.Processamento;

/// <summary>Metadado declarativo: associa uma implementação a uma categoria e versão.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ProcessadorPedidoAttribute(CategoriaProcessamento categoria, int versao = 1) : Attribute
{
    public CategoriaProcessamento Categoria { get; } = categoria;
    public int Versao { get; } = versao;
}

public sealed record ResultadoProcessamento(string Processador, string Detalhe);

public interface IProcessadorPedido
{
    Task<ResultadoProcessamento> ProcessarAsync(Pedido pedido, CancellationToken ct);
}

/// <summary>Estratégias concretas. O trabalho real é simulado com um pequeno atraso.</summary>
public static class Estrategias
{
    [ProcessadorPedido(CategoriaProcessamento.Padrao)]
    public sealed class ProcessadorPadrao : IProcessadorPedido
    {
        public async Task<ResultadoProcessamento> ProcessarAsync(Pedido pedido, CancellationToken ct)
        {
            await Task.Delay(5, ct);
            return new(nameof(ProcessadorPadrao), "separação e envio normal");
        }
    }

    [ProcessadorPedido(CategoriaProcessamento.Expresso)]
    public sealed class ProcessadorExpresso : IProcessadorPedido
    {
        public async Task<ResultadoProcessamento> ProcessarAsync(Pedido pedido, CancellationToken ct)
        {
            await Task.Delay(1, ct);
            return new(nameof(ProcessadorExpresso), "fila prioritária de expedição");
        }
    }

    // Uma mesma estratégia pode atender duas categorias (AllowMultiple = true)
    [ProcessadorPedido(CategoriaProcessamento.GrandeVolume)]
    [ProcessadorPedido(CategoriaProcessamento.AltoValor)]
    public sealed class ProcessadorConferenciaManual : IProcessadorPedido
    {
        public async Task<ResultadoProcessamento> ProcessarAsync(Pedido pedido, CancellationToken ct)
        {
            await Task.Delay(15, ct);
            return new(nameof(ProcessadorConferenciaManual), "conferência antes do envio");
        }
    }

    [ProcessadorPedido(CategoriaProcessamento.Internacional)]
    public sealed class ProcessadorInternacional : IProcessadorPedido
    {
        public async Task<ResultadoProcessamento> ProcessarAsync(Pedido pedido, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            return new(nameof(ProcessadorInternacional), "documentação alfandegária");
        }
    }
}
