namespace PortalTarefas.Web.Dominio;

public enum Prioridade { Baixa = 1, Media = 2, Alta = 3 }

/// <summary>Modelo de domínio da tarefa (não é exposto diretamente pela API nem pelas views).</summary>
public class Tarefa
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public Prioridade Prioridade { get; set; } = Prioridade.Media;
    public DateOnly? Prazo { get; set; }
    public string Situacao { get; set; } = Modelos.SituacaoTarefa.Pendente;
    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;

    public bool EstaVencida(DateOnly hoje) => Prazo < hoje && Situacao != Modelos.SituacaoTarefa.Concluida;
}
