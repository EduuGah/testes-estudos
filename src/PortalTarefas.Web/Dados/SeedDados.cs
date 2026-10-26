using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Modelos;

namespace PortalTarefas.Web.Dados;

/// <summary>
/// Seed idempotente: só insere quando a tabela está vazia. Reiniciar a aplicação
/// não duplica registros. Os dados são fictícios e servem aos testes da disciplina.
/// </summary>
public static class SeedDados
{
    public static async Task AplicarAsync(PortalTarefasDbContext db, CancellationToken ct = default)
    {
        if (await db.Tarefas.AnyAsync(ct)) return;

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        db.Tarefas.AddRange(
            new Tarefa { Titulo = "Configurar pipeline HTTP", Descricao = "Middleware e roteamento", Prioridade = Prioridade.Alta, Prazo = hoje.AddDays(-10), Situacao = SituacaoTarefa.Concluida },
            new Tarefa { Titulo = "Criar endpoints de tarefas", Descricao = "Binding de rota, query, header e corpo", Prioridade = Prioridade.Media, Prazo = hoje.AddDays(-2), Situacao = SituacaoTarefa.EmAndamento },
            new Tarefa { Titulo = "Comparar Minimal API e MVC", Prioridade = Prioridade.Baixa, Prazo = hoje.AddDays(5) },
            new Tarefa { Titulo = "Montar telas Razor", Descricao = "Layout, partial e Tag Helpers", Prioridade = Prioridade.Alta, Prazo = hoje.AddDays(3) },
            new Tarefa { Titulo = "Persistir tarefas com EF Core", Descricao = "Migrations, seed e concorrência", Prioridade = Prioridade.Alta, Prazo = hoje.AddDays(6), Situacao = SituacaoTarefa.EmAndamento },
            new Tarefa { Titulo = "Documentar a API", Prioridade = Prioridade.Media, Prazo = hoje.AddDays(12) });
        await db.SaveChangesAsync(ct);
    }
}
