using System.Collections.Concurrent;
using PortalTarefas.Web.Modelos;

namespace PortalTarefas.Web.Servicos;

/// <summary>
/// Armazenamento em memória usado nesta unidade. Será substituído por banco
/// relacional na Unidade 3, por isso os endpoints dependem apenas deste serviço.
/// </summary>
public class RepositorioTarefasMemoria
{
    private readonly ConcurrentDictionary<int, TarefaDto> _tarefas = new();
    private int _ultimoId;

    public RepositorioTarefasMemoria()
    {
        Adicionar(new CriarTarefaRequest { Titulo = "Configurar pipeline HTTP", Descricao = "Middleware e roteamento", Situacao = SituacaoTarefa.Concluida });
        Adicionar(new CriarTarefaRequest { Titulo = "Criar endpoints de tarefas", Descricao = "Binding de rota, query, header e corpo", Situacao = SituacaoTarefa.EmAndamento });
        Adicionar(new CriarTarefaRequest { Titulo = "Comparar Minimal API e MVC", Situacao = SituacaoTarefa.Pendente });
    }

    public IEnumerable<TarefaDto> Listar(string? situacao, string? busca) =>
        _tarefas.Values
            .Where(t => situacao is null || t.Situacao.Equals(situacao, StringComparison.OrdinalIgnoreCase))
            .Where(t => busca is null || t.Titulo.Contains(busca, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Id);

    public TarefaDto? Obter(int id) => _tarefas.GetValueOrDefault(id);

    public TarefaDto Adicionar(CriarTarefaRequest req)
    {
        var id = Interlocked.Increment(ref _ultimoId);
        var tarefa = new TarefaDto(id, req.Titulo.Trim(), req.Descricao, req.Situacao);
        _tarefas[id] = tarefa;
        return tarefa;
    }

    public int Total => _tarefas.Count;
}
