using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.Validacao;

/// <summary>
/// Validação customizada que envolve mais de um campo (prioridade + prazo):
/// tarefas de prioridade Alta precisam de prazo, e esse prazo deve estar entre
/// hoje e os próximos 7 dias. Prazo no passado nunca é aceito em um cadastro.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PrazoCompativelAttribute : ValidationAttribute
{
    public const int DiasMaximosAltaPrioridade = 7;
    public Func<DateOnly> Hoje { get; set; } = () => DateOnly.FromDateTime(DateTime.Today);

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not TarefaInputModel modelo) return ValidationResult.Success;
        var hoje = Hoje();
        string[] campo = [nameof(TarefaInputModel.Prazo)];

        if (modelo.Prioridade == Prioridade.Alta && modelo.Prazo is null)
            return new ValidationResult("Tarefas de prioridade Alta precisam de prazo.", campo);

        if (modelo.Prazo is { } prazo && prazo < hoje && modelo.Id == 0)
            return new ValidationResult("O prazo não pode estar no passado.", campo);

        if (modelo.Prioridade == Prioridade.Alta && modelo.Prazo > hoje.AddDays(DiasMaximosAltaPrioridade))
            return new ValidationResult($"Para prioridade Alta o prazo deve ser de no máximo {DiasMaximosAltaPrioridade} dias.", campo);

        return ValidationResult.Success;
    }
}
