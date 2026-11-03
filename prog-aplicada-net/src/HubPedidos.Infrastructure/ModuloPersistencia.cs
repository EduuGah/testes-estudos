using HubPedidos.Application.Contratos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HubPedidos.Infrastructure;

public static class ModuloPersistencia
{
    public static IServiceCollection AddModuloPersistencia(this IServiceCollection services, IConfiguration configuracao, IHostEnvironment ambiente)
    {
        var conexao = configuracao.GetConnectionString("Hub") ?? "Data Source=hub.db";

        // proteção: em testes, só banco em memória (nunca o arquivo de desenvolvimento)
        if (ambiente.IsEnvironment("Testes") && !conexao.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O ambiente de testes só aceita SQLite em memória.");

        services.AddDbContext<HubDbContext>(o =>
        {
            o.UseSqlite(conexao);
            if (configuracao.GetValue<bool>("Hub:LogarSql")) o.LogTo(Console.WriteLine, [DbLoggerCategory.Database.Command.Name]);
        });
        services.AddScoped<IPedidoRepositorio, RepositorioPedidos>();
        services.AddScoped<RepositorioPedidos>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    /// <summary>Aplica as migrations e ativa WAL (leituras não bloqueiam a escrita no SQLite).</summary>
    public static async Task PrepararBancoAsync(IServiceProvider servicos, CancellationToken ct = default)
    {
        await using var escopo = servicos.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<HubDbContext>();
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync(ct);
        else await db.Database.EnsureCreatedAsync(ct);
        if (db.Database.GetDbConnection() is SqliteConnection c && !c.ConnectionString.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }
}
