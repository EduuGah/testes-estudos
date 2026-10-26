using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Dados.Configuracoes;

/// <summary>Mapeamento por Fluent API: chave, limites, obrigatoriedade, índices e concorrência.</summary>
public class TarefaConfiguration : IEntityTypeConfiguration<Tarefa>
{
    public void Configure(EntityTypeBuilder<Tarefa> builder)
    {
        builder.ToTable("Tarefas");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Titulo).HasMaxLength(80).IsRequired();
        builder.Property(t => t.Descricao).HasMaxLength(500);
        builder.Property(t => t.Situacao).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Prioridade).HasConversion<int>().IsRequired();
        builder.Property(t => t.Prazo).HasColumnType("date");
        builder.Property(t => t.CriadaEmUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(t => t.RowVersion).IsRowVersion();   // SQL Server gera um novo valor a cada UPDATE

        builder.HasIndex(t => new { t.Situacao, t.Prazo }).HasDatabaseName("IX_Tarefas_Situacao_Prazo");
    }
}

public class EventoAuditoriaConfiguration : IEntityTypeConfiguration<EventoAuditoria>
{
    public void Configure(EntityTypeBuilder<EventoAuditoria> builder)
    {
        builder.ToTable("EventosAuditoria");
        builder.Property(e => e.Acao).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Detalhe).HasMaxLength(300);
        builder.HasOne<Tarefa>().WithMany().HasForeignKey(e => e.TarefaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.TarefaId);
    }
}
