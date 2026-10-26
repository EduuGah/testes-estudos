namespace PortalTarefas.Web.Dominio;

public enum Prioridade { Baixa = 1, Media = 2, Alta = 3 }

/// <summary>Entidade persistida da tarefa.</summary>
public class Tarefa
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public Prioridade Prioridade { get; set; } = Prioridade.Media;
    public DateOnly? Prazo { get; set; }
    public string Situacao { get; set; } = Modelos.SituacaoTarefa.Pendente;
    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadaEmUtc { get; set; }

    /// <summary>Equipe dona da tarefa (Unidade 6: usada na política de edição).</summary>
    public string Equipe { get; set; } = "Geral";

    /// <summary>Id do usuário que criou a tarefa (extensão: política por proprietário).</summary>
    public string? ProprietarioId { get; set; }

    /// <summary>Token de concorrência otimista (rowversion no SQL Server).</summary>
    public byte[] RowVersion { get; set; } = [];

    public bool EstaVencida(DateOnly hoje) => Prazo < hoje && Situacao != Modelos.SituacaoTarefa.Concluida;
}

/// <summary>Registro de auditoria gravado na mesma transação da alteração.</summary>
public class EventoAuditoria
{
    public long Id { get; set; }
    public int TarefaId { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string? Detalhe { get; set; }
    public DateTime OcorridoEmUtc { get; set; } = DateTime.UtcNow;
}
