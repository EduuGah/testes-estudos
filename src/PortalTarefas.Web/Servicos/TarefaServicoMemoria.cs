using System.Collections.Concurrent;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Modelos;

namespace PortalTarefas.Web.Servicos;

/// <summary>Implementação em memória (singleton) usada nas Unidades 1 e 2.</summary>
public class TarefaServicoMemoria : ITarefaServico
{
    private readonly ConcurrentDictionary<int, Tarefa> _tarefas = new();
    private int _ultimoId;

    public TarefaServicoMemoria()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        Semear(new Tarefa { Titulo = "Configurar pipeline HTTP", Descricao = "Middleware e roteamento", Prioridade = Prioridade.Alta, Prazo = hoje.AddDays(-10), Situacao = SituacaoTarefa.Concluida });
        Semear(new Tarefa { Titulo = "Criar endpoints de tarefas", Descricao = "Binding de rota, query, header e corpo", Prioridade = Prioridade.Media, Prazo = hoje.AddDays(-2), Situacao = SituacaoTarefa.EmAndamento });
        Semear(new Tarefa { Titulo = "Comparar Minimal API e MVC", Prioridade = Prioridade.Baixa, Prazo = hoje.AddDays(5) });
        Semear(new Tarefa { Titulo = "Montar telas Razor", Descricao = "Layout, partial e Tag Helpers", Prioridade = Prioridade.Alta, Prazo = hoje.AddDays(3) });
    }

    private void Semear(Tarefa t) { t.Id = Interlocked.Increment(ref _ultimoId); _tarefas[t.Id] = t; }

    public Task<IReadOnlyList<Tarefa>> ListarAsync(string? situacao = null, string? busca = null, CancellationToken ct = default)
    {
        IReadOnlyList<Tarefa> lista = _tarefas.Values
            .Where(t => string.IsNullOrEmpty(situacao) || t.Situacao.Equals(situacao, StringComparison.OrdinalIgnoreCase))
            .Where(t => string.IsNullOrEmpty(busca) || t.Titulo.Contains(busca, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Id).ToList();
        return Task.FromResult(lista);
    }

    public Task<Tarefa?> ObterAsync(int id, CancellationToken ct = default) => Task.FromResult(_tarefas.GetValueOrDefault(id));

    public Task<Tarefa> CriarAsync(Tarefa tarefa, CancellationToken ct = default)
    {
        Semear(tarefa);
        return Task.FromResult(tarefa);
    }

    public Task<bool> AtualizarAsync(Tarefa tarefa, CancellationToken ct = default)
    {
        if (!_tarefas.ContainsKey(tarefa.Id)) return Task.FromResult(false);
        _tarefas[tarefa.Id] = tarefa;
        return Task.FromResult(true);
    }

    public Task<int> ContarAsync(CancellationToken ct = default) => Task.FromResult(_tarefas.Count);
}
