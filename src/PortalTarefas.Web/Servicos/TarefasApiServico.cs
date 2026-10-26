using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Contratos;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Servicos;

/// <summary>Serviço de aplicação da API: monta consultas no banco e converte para DTOs.</summary>
public class TarefasApiServico(PortalTarefasDbContext db, ITarefaServico tarefas)
{
    public async Task<PaginaDto<TarefaResumoDto>> ConsultarAsync(ConsultaTarefas q, CancellationToken ct)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        IQueryable<Tarefa> consulta = db.Tarefas.AsNoTracking();

        // 1) filtros (viram WHERE)
        if (q.Situacao is not null) consulta = consulta.Where(t => t.Situacao == q.Situacao);
        if (q.Prioridade is not null) consulta = consulta.Where(t => t.Prioridade == q.Prioridade);
        if (!string.IsNullOrWhiteSpace(q.Busca)) consulta = consulta.Where(t => t.Titulo.Contains(q.Busca));

        var total = await consulta.CountAsync(ct);

        // 2) ordenação a partir da lista permitida (nenhum texto do usuário vai para o SQL)
        var desc = q.Direcao == "desc";
        consulta = q.OrdenarPor.ToLowerInvariant() switch
        {
            "titulo" => desc ? consulta.OrderByDescending(t => t.Titulo) : consulta.OrderBy(t => t.Titulo),
            "prazo" => desc ? consulta.OrderByDescending(t => t.Prazo) : consulta.OrderBy(t => t.Prazo),
            "prioridade" => desc ? consulta.OrderByDescending(t => t.Prioridade) : consulta.OrderBy(t => t.Prioridade),
            "situacao" => desc ? consulta.OrderByDescending(t => t.Situacao) : consulta.OrderBy(t => t.Situacao),
            _ => desc ? consulta.OrderByDescending(t => t.Id) : consulta.OrderBy(t => t.Id)
        };

        // 3) recorte da página + projeção para o DTO antes de materializar
        var itens = await consulta
            .Skip((q.Pagina - 1) * q.Tamanho)
            .Take(q.Tamanho)
            .Select(t => new TarefaResumoDto(t.Id, t.Titulo, t.Prioridade, t.Prazo, t.Situacao,
                t.Prazo < hoje && t.Situacao != Modelos.SituacaoTarefa.Concluida))
            .ToListAsync(ct);

        return new PaginaDto<TarefaResumoDto>(itens, q.Pagina, q.Tamanho, total, (int)Math.Ceiling(total / (double)q.Tamanho));
    }

    public async Task<TarefaDetalheDto?> ObterAsync(int id, CancellationToken ct) =>
        await tarefas.ObterAsync(id, ct) is { } t ? ParaDetalhe(t) : null;

    public async Task<TarefaDetalheDto> CriarAsync(CriarTarefaDto dto, CancellationToken ct)
    {
        var tarefa = await tarefas.CriarAsync(new Tarefa
        {
            Titulo = dto.Titulo.Trim(), Descricao = dto.Descricao, Prioridade = dto.Prioridade, Prazo = dto.Prazo
        }, ct);
        return ParaDetalhe(tarefa);
    }

    /// <returns>null quando a tarefa não existe.</returns>
    public async Task<TarefaDetalheDto?> AtualizarAsync(int id, AtualizarTarefaDto dto, CancellationToken ct)
    {
        var alteracoes = new Tarefa
        {
            Id = id, Titulo = dto.Titulo.Trim(), Descricao = dto.Descricao, Prioridade = dto.Prioridade,
            Prazo = dto.Prazo, Situacao = dto.Situacao
        };
        if (!await tarefas.AtualizarAsync(alteracoes, Convert.FromBase64String(dto.Versao), ct)) return null;
        return await ObterAsync(id, ct);
    }

    public async Task<bool> ExcluirAsync(int id, CancellationToken ct) =>
        await db.Tarefas.Where(t => t.Id == id).ExecuteDeleteAsync(ct) > 0;

    public static Dictionary<string, string[]> ValidarDto(object dto)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, true);
        return resultados
            .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(g => g.Key.Length > 0 ? char.ToLowerInvariant(g.Key[0]) + g.Key[1..] : g.Key,
                          g => g.Select(r => r.ErrorMessage ?? "Valor inválido.").ToArray());
    }

    private static TarefaDetalheDto ParaDetalhe(Tarefa t) =>
        new(t.Id, t.Titulo, t.Descricao, t.Prioridade, t.Prazo, t.Situacao, t.CriadaEmUtc, t.AtualizadaEmUtc,
            Convert.ToBase64String(t.RowVersion));
}
