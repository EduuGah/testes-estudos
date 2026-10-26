using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Contratos;

// ---------------- Contrato público da API v1 ----------------
// A entidade Tarefa nunca sai da aplicação: cada operação usa um DTO próprio.

/// <summary>Dados para criar uma tarefa.</summary>
public record CriarTarefaDto(
    [property: Required, StringLength(80, MinimumLength = 3), Description("Título exibido na lista")] string Titulo,
    [property: StringLength(500)] string? Descricao,
    Prioridade Prioridade,
    DateOnly? Prazo);

/// <summary>Dados para atualizar uma tarefa. Exige o token de concorrência lido anteriormente.</summary>
public record AtualizarTarefaDto(
    [property: Required, StringLength(80, MinimumLength = 3)] string Titulo,
    [property: StringLength(500)] string? Descricao,
    Prioridade Prioridade,
    DateOnly? Prazo,
    [property: Required] string Situacao,
    [property: Required, Description("Valor de 'versao' devolvido no GET de detalhes")] string Versao);

/// <summary>Item resumido usado na listagem paginada.</summary>
public record TarefaResumoDto(int Id, string Titulo, Prioridade Prioridade, DateOnly? Prazo, string Situacao, bool Vencida);

/// <summary>Representação completa de uma tarefa.</summary>
public record TarefaDetalheDto(
    int Id, string Titulo, string? Descricao, Prioridade Prioridade, DateOnly? Prazo, string Situacao,
    DateTime CriadaEmUtc, DateTime? AtualizadaEmUtc,
    [property: Description("Token de concorrência otimista (Base64)")] string Versao);

/// <summary>Envelope de paginação.</summary>
public record PaginaDto<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int TotalItens, int TotalPaginas);

/// <summary>Parâmetros de consulta da listagem (query string).</summary>
public class ConsultaTarefas
{
    public const int TamanhoPadrao = 10;
    public const int TamanhoMaximo = 50;

    /// <summary>Campos aceitos em ordenarPor (lista permitida).</summary>
    public static readonly string[] CamposOrdenaveis = ["id", "titulo", "prazo", "prioridade", "situacao"];

    [Description("Número da página (começa em 1)")] public int Pagina { get; init; } = 1;
    [Description("Itens por página (1 a 50)")] public int Tamanho { get; init; } = TamanhoPadrao;
    [Description("id | titulo | prazo | prioridade | situacao")] public string OrdenarPor { get; init; } = "id";
    [Description("asc | desc")] public string Direcao { get; init; } = "asc";
    [Description("Filtra pela situação")] public string? Situacao { get; init; }
    [Description("Filtra pela prioridade")] public Prioridade? Prioridade { get; init; }
    [Description("Busca parcial no título")] public string? Busca { get; init; }

    public Dictionary<string, string[]> Validar()
    {
        var erros = new Dictionary<string, string[]>();
        if (Pagina < 1) erros["pagina"] = ["A página deve ser maior ou igual a 1."];
        if (Tamanho is < 1 or > TamanhoMaximo) erros["tamanho"] = [$"O tamanho deve estar entre 1 e {TamanhoMaximo}."];
        if (!CamposOrdenaveis.Contains(OrdenarPor.ToLowerInvariant()))
            erros["ordenarPor"] = [$"Campo não permitido. Use: {string.Join(", ", CamposOrdenaveis)}."];
        if (Direcao is not ("asc" or "desc")) erros["direcao"] = ["Use asc ou desc."];
        if (Situacao is not null && !Modelos.SituacaoTarefa.Todas.Contains(Situacao))
            erros["situacao"] = [$"Situação inválida. Use: {string.Join(", ", Modelos.SituacaoTarefa.Todas)}."];
        return erros;
    }
}
