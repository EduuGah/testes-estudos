using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Servicos;

/// <summary>Implementação assíncrona com EF Core (substitui TarefaServicoMemoria).</summary>
public class TarefaServicoEf(PortalTarefasDbContext db, ILogger<TarefaServicoEf> logger) : ITarefaServico
{
    public async Task<IReadOnlyList<Tarefa>> ListarAsync(string? situacao = null, string? busca = null, CancellationToken ct = default)
    {
        IQueryable<Tarefa> consulta = db.Tarefas.AsNoTracking();
        if (!string.IsNullOrEmpty(situacao)) consulta = consulta.Where(t => t.Situacao == situacao);
        if (!string.IsNullOrEmpty(busca)) consulta = consulta.Where(t => t.Titulo.Contains(busca));
        return await consulta.OrderBy(t => t.Id).ToListAsync(ct);   // filtros viram WHERE no SQL
    }

    public Task<Tarefa?> ObterAsync(int id, CancellationToken ct = default) =>
        db.Tarefas.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Tarefa> CriarAsync(Tarefa tarefa, CancellationToken ct = default)
    {
        db.Tarefas.Add(tarefa);
        await db.SaveChangesAsync(ct);
        return tarefa;
    }

    public async Task<bool> AtualizarAsync(Tarefa alteracoes, byte[] rowVersionOriginal, CancellationToken ct = default)
    {
        var tarefa = await db.Tarefas.FirstOrDefaultAsync(t => t.Id == alteracoes.Id, ct);
        if (tarefa is null) return false;

        // Informa ao EF a versão que o usuário viu; o UPDATE terá "WHERE Id = @id AND RowVersion = @original"
        db.Entry(tarefa).Property(t => t.RowVersion).OriginalValue = rowVersionOriginal;

        tarefa.Titulo = alteracoes.Titulo;
        tarefa.Descricao = alteracoes.Descricao;
        tarefa.Prioridade = alteracoes.Prioridade;
        tarefa.Prazo = alteracoes.Prazo;
        tarefa.Situacao = alteracoes.Situacao;
        tarefa.AtualizadaEmUtc = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Estratégia explícita: "o primeiro a salvar vence". Nada é sobrescrito;
            // devolvemos os valores atuais para o usuário revisar e decidir.
            var entrada = ex.Entries.Single();
            var valoresBanco = await entrada.GetDatabaseValuesAsync(ct);
            logger.LogWarning("Conflito de concorrência na tarefa {TarefaId}", alteracoes.Id);
            throw new ConflitoConcorrenciaException(alteracoes.Id, (Tarefa?)valoresBanco?.ToObject());
        }
    }

    public async Task AlterarSituacaoAsync(int id, string novaSituacao, CancellationToken ct = default)
    {
        // Transação explícita: alteração + auditoria são confirmadas juntas ou desfeitas juntas.
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var tarefa = await db.Tarefas.SingleAsync(t => t.Id == id, ct);
            var anterior = tarefa.Situacao;
            tarefa.Situacao = novaSituacao;
            tarefa.AtualizadaEmUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            db.EventosAuditoria.Add(new EventoAuditoria
            {
                TarefaId = id,
                Acao = "AlterarSituacao",
                Detalhe = $"{anterior} -> {novaSituacao}"
            });
            await db.SaveChangesAsync(ct);

            await transacao.CommitAsync(ct);
        }
        catch
        {
            await transacao.RollbackAsync(ct);
            throw;
        }
    }

    public Task<int> ContarAsync(CancellationToken ct = default) => db.Tarefas.CountAsync(ct);
}
