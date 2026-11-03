using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HubPedidos.Infrastructure;

// Entidades de persistência: separadas do domínio (o domínio não conhece EF Core).
public sealed class PedidoEntidade
{
    public Guid Id { get; set; }
    public string Cliente { get; set; } = "";
    public string Regiao { get; set; } = "";
    public string Prioridade { get; set; } = "";
    public string Status { get; set; } = "";
    /// <summary>Valores em centavos: o SQLite não ordena nem soma decimal no servidor.</summary>
    public long TotalCentavos { get; set; }
    public DateTime CriadoEmUtc { get; set; }
    public DateTime AtualizadoEmUtc { get; set; }
    public int Versao { get; set; }
    public List<ItemEntidade> Itens { get; set; } = [];
    public MetadadosEntidade Metadados { get; set; } = new();
}

public sealed class ItemEntidade
{
    public int Id { get; set; }
    public Guid PedidoId { get; set; }
    public string Sku { get; set; } = "";
    public int Quantidade { get; set; }
    public long PrecoCentavos { get; set; }
}

/// <summary>Agregado pequeno guardado como JSON na própria linha do pedido.</summary>
public sealed class MetadadosEntidade
{
    public string Canal { get; set; } = "api";
    public string? Cupom { get; set; }
    public List<string> Tags { get; set; } = [];
}

public sealed class HubDbContext(DbContextOptions<HubDbContext> options) : DbContext(options)
{
    public DbSet<PedidoEntidade> Pedidos => Set<PedidoEntidade>();
    public DbSet<ItemEntidade> Itens => Set<ItemEntidade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PedidoEntidade>(p =>
        {
            p.ToTable("Pedidos");
            p.HasKey(x => x.Id);
            p.Property(x => x.Cliente).HasMaxLength(80).IsRequired();
            p.Property(x => x.Regiao).HasMaxLength(20).IsRequired();
            p.Property(x => x.Prioridade).HasMaxLength(10).IsRequired();
            p.Property(x => x.Status).HasMaxLength(20).IsRequired();
            p.Property(x => x.Versao).IsConcurrencyToken();
            // índices pensados para o painel (filtro por status + ordenação por data) e para "meus pedidos"
            p.HasIndex(x => new { x.Status, x.CriadoEmUtc });
            p.HasIndex(x => new { x.Cliente, x.CriadoEmUtc });
            p.HasMany(x => x.Itens).WithOne().HasForeignKey(i => i.PedidoId).OnDelete(DeleteBehavior.Cascade);
            p.OwnsOne(x => x.Metadados, m => m.ToJson("Metadados"));
        });

        modelBuilder.Entity<ItemEntidade>(i =>
        {
            i.ToTable("ItensPedido");
            i.HasKey(x => x.Id);
            i.Property(x => x.Sku).HasMaxLength(40).IsRequired();
            i.HasIndex(x => x.PedidoId);
        });
    }
}

/// <summary>Usado pelo dotnet ef (migrations) sem precisar subir a API.</summary>
public sealed class HubDbContextFactory : IDesignTimeDbContextFactory<HubDbContext>
{
    public HubDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<HubDbContext>().UseSqlite("Data Source=hub-design.db").Options);
}
