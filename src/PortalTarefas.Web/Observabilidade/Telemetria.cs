using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PortalTarefas.Web.Observabilidade;

/// <summary>
/// Fonte de traces e medidor de métricas da aplicação. As dimensões (tags) usam
/// valores de um conjunto pequeno e conhecido para manter a cardinalidade controlada:
/// nunca id de usuário, e-mail ou texto livre.
/// </summary>
public sealed class Telemetria : IDisposable
{
    public const string Nome = "PortalTarefas";
    public static readonly ActivitySource Fonte = new(Nome, "1.0.0");

    private readonly Meter _medidor;

    /// <summary>Métrica de negócio: tarefas criadas, por prioridade (3 valores possíveis).</summary>
    public Counter<long> TarefasCriadas { get; }

    /// <summary>Métrica técnica: duração das chamadas ao serviço de dicas, por resultado (sucesso|falha|circuito_aberto).</summary>
    public Histogram<double> DuracaoServicoDicas { get; }

    public Telemetria(IMeterFactory fabrica)
    {
        _medidor = fabrica.Create(Nome, "1.0.0");
        TarefasCriadas = _medidor.CreateCounter<long>("portal.tarefas.criadas", unit: "{tarefa}",
            description: "Quantidade de tarefas criadas");
        DuracaoServicoDicas = _medidor.CreateHistogram<double>("portal.dicas.duracao", unit: "ms",
            description: "Duração das chamadas ao serviço externo de dicas");
    }

    public void Dispose() => _medidor.Dispose();
}
