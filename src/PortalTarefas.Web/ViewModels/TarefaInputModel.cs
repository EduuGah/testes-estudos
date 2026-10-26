using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Modelos;
using PortalTarefas.Web.Validacao;

namespace PortalTarefas.Web.ViewModels;

/// <summary>ViewModel de entrada dos formulários de cadastro e edição.</summary>
[PrazoCompativel]
public class TarefaInputModel
{
    public int Id { get; set; }

    [Display(Name = "Título")]
    [Required(ErrorMessage = "Informe o título da tarefa.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "O título deve ter entre {2} e {1} caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [Display(Name = "Descrição")]
    [StringLength(500, ErrorMessage = "A descrição pode ter no máximo {1} caracteres.")]
    public string? Descricao { get; set; }

    [Display(Name = "Prioridade")]
    [EnumDataType(typeof(Prioridade), ErrorMessage = "Selecione uma prioridade válida.")]
    public Prioridade Prioridade { get; set; } = Prioridade.Media;

    [Display(Name = "Prazo")]
    [DataType(DataType.Date)]
    public DateOnly? Prazo { get; set; }

    [Display(Name = "Situação")]
    public string Situacao { get; set; } = SituacaoTarefa.Pendente;

    /// <summary>Token de concorrência (Base64) enviado em campo oculto na edição.</summary>
    public string? RowVersion { get; set; }

    public static TarefaInputModel De(Tarefa t) => new()
    {
        Id = t.Id, Titulo = t.Titulo, Descricao = t.Descricao, Prioridade = t.Prioridade, Prazo = t.Prazo, Situacao = t.Situacao,
        RowVersion = Convert.ToBase64String(t.RowVersion)
    };

    public void AplicarEm(Tarefa t)
    {
        t.Titulo = Titulo.Trim(); t.Descricao = Descricao; t.Prioridade = Prioridade; t.Prazo = Prazo; t.Situacao = Situacao;
    }
}

/// <summary>ViewModel de leitura usado na lista, nos detalhes e na partial de resumo.</summary>
public record TarefaResumoViewModel(int Id, string Titulo, string? Descricao, Prioridade Prioridade, DateOnly? Prazo, string Situacao, bool Vencida)
{
    public static TarefaResumoViewModel De(Tarefa t) =>
        new(t.Id, t.Titulo, t.Descricao, t.Prioridade, t.Prazo, t.Situacao, t.EstaVencida(DateOnly.FromDateTime(DateTime.Today)));
}

public record ListaTarefasViewModel(IReadOnlyList<TarefaResumoViewModel> Tarefas, string? Situacao, string? Busca);
