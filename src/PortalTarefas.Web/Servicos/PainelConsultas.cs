using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Modelos;

namespace PortalTarefas.Web.Servicos;

/// <summary>Linha do painel: quantidade de tarefas e de vencidas por situação.</summary>
public record LinhaPainel(string Situacao, int Quantidade, int Vencidas);

/// <summary>
/// A mesma leitura do painel implementada de duas formas para comparação:
/// EF Core (projeção + AsNoTracking) e Dapper (SQL manual com parâmetros).
/// </summary>
public class PainelConsultas(PortalTarefasDbContext db, IConfiguration configuracao)
{
    public async Task<IReadOnlyList<LinhaPainel>> ComEfCoreAsync(DateOnly hoje, CancellationToken ct = default)
    {
        // Agregação feita no banco (GROUP BY + SUM com CASE); só o resultado vem para a memória.
        // Observação: g.Count(condição) dentro do GroupBy não foi traduzido para SQL pelo provedor,
        // por isso a contagem de vencidas usa Sum com expressão condicional.
        var linhas = await db.Tarefas
            .AsNoTracking()
            .GroupBy(t => t.Situacao)
            .Select(g => new
            {
                Situacao = g.Key,
                Quantidade = g.Count(),
                Vencidas = g.Sum(t => t.Prazo < hoje && t.Situacao != SituacaoTarefa.Concluida ? 1 : 0)
            })
            .OrderBy(l => l.Situacao)
            .ToListAsync(ct);
        return linhas.Select(l => new LinhaPainel(l.Situacao, l.Quantidade, l.Vencidas)).ToList();
    }

    public async Task<IReadOnlyList<LinhaPainel>> ComDapperAsync(DateOnly hoje, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Situacao,
                   COUNT(*) AS Quantidade,
                   SUM(CASE WHEN Prazo < @Hoje AND Situacao <> @Concluida THEN 1 ELSE 0 END) AS Vencidas
            FROM Tarefas
            GROUP BY Situacao
            ORDER BY Situacao
            """;
        await using var conexao = new SqlConnection(configuracao.GetConnectionString("PortalTarefas"));
        // Valores sempre como parâmetros (@Hoje, @Concluida): nunca concatenados no SQL
        var linhas = await conexao.QueryAsync<LinhaPainel>(new CommandDefinition(
            sql,
            new { Hoje = hoje.ToDateTime(TimeOnly.MinValue), Concluida = SituacaoTarefa.Concluida },
            cancellationToken: ct));
        return linhas.ToList();
    }
}
