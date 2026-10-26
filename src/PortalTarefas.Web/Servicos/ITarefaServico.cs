using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Servicos;

/// <summary>Contrato usado por controllers e endpoints; isola a apresentação da persistência.</summary>
public interface ITarefaServico
{
    Task<IReadOnlyList<Tarefa>> ListarAsync(string? situacao = null, string? busca = null, CancellationToken ct = default);
    Task<Tarefa?> ObterAsync(int id, CancellationToken ct = default);
    Task<Tarefa> CriarAsync(Tarefa tarefa, CancellationToken ct = default);

    /// <summary>
    /// Atualiza usando o token de concorrência que o cliente leu.
    /// Lança <see cref="ConflitoConcorrenciaException"/> se outro usuário salvou antes.
    /// </summary>
    Task<bool> AtualizarAsync(Tarefa alteracoes, byte[] rowVersionOriginal, CancellationToken ct = default);

    /// <summary>Altera a situação e grava auditoria na mesma transação.</summary>
    Task AlterarSituacaoAsync(int id, string novaSituacao, CancellationToken ct = default);

    Task<int> ContarAsync(CancellationToken ct = default);
}

public class ConflitoConcorrenciaException(int tarefaId, Tarefa? valoresAtuais)
    : Exception($"A tarefa {tarefaId} foi alterada por outra sessão.")
{
    public int TarefaId { get; } = tarefaId;
    public Tarefa? ValoresAtuais { get; } = valoresAtuais;
}
