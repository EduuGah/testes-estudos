using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Servicos;

/// <summary>
/// Contrato usado por controllers e endpoints. A interface isola a camada de
/// apresentação da persistência: na Unidade 3 a implementação em memória dá
/// lugar a uma implementação com banco de dados sem alterar quem a consome.
/// </summary>
public interface ITarefaServico
{
    Task<IReadOnlyList<Tarefa>> ListarAsync(string? situacao = null, string? busca = null, CancellationToken ct = default);
    Task<Tarefa?> ObterAsync(int id, CancellationToken ct = default);
    Task<Tarefa> CriarAsync(Tarefa tarefa, CancellationToken ct = default);
    Task<bool> AtualizarAsync(Tarefa tarefa, CancellationToken ct = default);
    Task<int> ContarAsync(CancellationToken ct = default);
}
