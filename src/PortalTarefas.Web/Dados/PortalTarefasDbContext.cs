using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Dados;

/// <summary>
/// Contexto do EF Core. Registrado com AddDbContextPool: por isso não guarda
/// estado próprio em campos (usuário atual, caches etc.) — a instância é limpa
/// e reaproveitada entre requisições.
/// </summary>
public class PortalTarefasDbContext(DbContextOptions<PortalTarefasDbContext> options)
    : IdentityDbContext<UsuarioApp>(options)   // Unidade 6: tabelas AspNetUsers, AspNetRoles, AspNetUserClaims...
{
    public DbSet<Tarefa> Tarefas => Set<Tarefa>();
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // obrigatório: configura o esquema do Identity
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PortalTarefasDbContext).Assembly);
    }
}
