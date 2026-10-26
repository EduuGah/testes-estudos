using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.Modelos;

/// <summary>Situações possíveis de uma tarefa no Portal.</summary>
public static class SituacaoTarefa
{
    public const string Pendente = "Pendente";
    public const string EmAndamento = "EmAndamento";
    public const string Concluida = "Concluida";
    public static readonly string[] Todas = [Pendente, EmAndamento, Concluida];
}

/// <summary>DTO de saída: o que a API devolve ao cliente.</summary>
public record TarefaDto(int Id, string Titulo, string? Descricao, string Situacao);

/// <summary>DTO de entrada para cadastro, recebido pelo corpo JSON.</summary>
public class CriarTarefaRequest
{
    [Required(ErrorMessage = "O título é obrigatório.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "O título deve ter entre 3 e 80 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "A descrição pode ter no máximo 500 caracteres.")]
    public string? Descricao { get; set; }

    public string Situacao { get; set; } = SituacaoTarefa.Pendente;
}
