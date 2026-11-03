using System.Data.Common;
using HubPedidos.Application.Contratos;
using HubPedidos.Domain;
using HubPedidos.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HubPedidos.Tests;

/// <summary>Captura os comandos SQL enviados ao banco (texto e quantidade).</summary>
public sealed class CapturaSql : DbCommandInterceptor
{
    public List<string> Comandos { get; } = [];

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Comandos.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Comandos.Add(command.CommandText);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Comandos.Add(command.CommandText);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}

public sealed class BancoEfemero : IAsyncDisposable
{
    // a conexão fica aberta durante todo o teste: fechar a última conexão apaga o banco em memória
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    public CapturaSql Captura { get; } = new();

    public static async Task<BancoEfemero> CriarAsync(int pedidos)
    {
        var b = new BancoEfemero();
        await b._conexao.OpenAsync();
        await using var db = b.Contexto(capturar: false);
        await db.Database.EnsureCreatedAsync();
        var repo = new RepositorioPedidos(db, TimeProvider.System);
        for (var i = 0; i < pedidos; i++)
        {
            var p = new Pedido(PedidoId.Novo(), i % 2 == 0 ? "ana" : "bruno", Regiao.Sudeste, Prioridade.Normal,
                [new ItemPedido("A", 1, 10m + i), new ItemPedido("B", 2, 5m)]);
            await repo.AdicionarAsync(p, new MetadadosPedido(i % 3 == 0 ? "app" : "site", null, ["teste"]), default);
            db.ChangeTracker.Clear();
        }
        return b;
    }

    public HubDbContext Contexto(bool capturar = true)
    {
        var o = new DbContextOptionsBuilder<HubDbContext>().UseSqlite(_conexao);
        if (capturar) o.AddInterceptors(Captura);
        return new HubDbContext(o.Options);
    }

    public async ValueTask DisposeAsync() => await _conexao.DisposeAsync();
}

public class EfCoreTests
{
    [Fact]
    public async Task Painel_usa_dois_comandos_e_so_as_colunas_necessarias()
    {
        await using var banco = await BancoEfemero.CriarAsync(30);
        await using var db = banco.Contexto();
        var pagina = await new RepositorioPedidos(db, TimeProvider.System).ListarAsync(new FiltroPainel(null, 2, 10), default);

        Assert.Equal(10, pagina.Itens.Count);
        Assert.Equal(30, pagina.Total);
        Assert.Equal(2, banco.Captura.Comandos.Count);                  // COUNT + página (sem N+1)
        Assert.DoesNotContain("Metadados", banco.Captura.Comandos[1]);  // projeção: o JSON não é lido
        Assert.Empty(db.ChangeTracker.Entries());                       // AsNoTracking
    }

    [Fact]
    public async Task Versao_ingenua_mostra_o_problema_N_mais_1()
    {
        await using var banco = await BancoEfemero.CriarAsync(30);
        await using var db = banco.Contexto();
        await new RepositorioPedidos(db, TimeProvider.System).ListarIngenuoAsync(new FiltroPainel(null, 1, 10), default);
        Assert.Equal(11, banco.Captura.Comandos.Count);   // 1 consulta de pedidos + 10 de itens
    }

    [Fact]
    public async Task Atualizacao_usa_tracking_e_altera_so_as_colunas_mudadas()
    {
        await using var banco = await BancoEfemero.CriarAsync(1);
        await using var db = banco.Contexto();
        var id = await db.Pedidos.Select(p => p.Id).FirstAsync();
        banco.Captura.Comandos.Clear();

        var versao = await new RepositorioPedidos(db, TimeProvider.System).AtualizarStatusAsync(new PedidoId(id), StatusPedido.Processado, 1, default);

        Assert.Equal(2, versao);
        var update = Assert.Single(banco.Captura.Comandos, c => c.Contains("UPDATE", StringComparison.Ordinal));
        Assert.Contains("\"Status\"", update);
        Assert.DoesNotContain("\"Cliente\"", update);
        Assert.Contains("\"Versao\" = @", update);   // token de concorrência no WHERE
    }

    [Fact]
    public async Task Versao_obsoleta_gera_conflito()
    {
        await using var banco = await BancoEfemero.CriarAsync(1);
        await using var db = banco.Contexto();
        var id = await db.Pedidos.Select(p => p.Id).FirstAsync();
        await Assert.ThrowsAsync<ConflitoVersaoException>(() =>
            new RepositorioPedidos(db, TimeProvider.System).AtualizarStatusAsync(new PedidoId(id), StatusPedido.Cancelado, 99, default));
    }

    [Fact]
    public async Task Parametros_do_usuario_nao_mudam_a_forma_do_SQL()
    {
        await using var banco = await BancoEfemero.CriarAsync(5);
        await using var db = banco.Contexto();
        var repo = new RepositorioPedidos(db, TimeProvider.System);
        await repo.ListarAsync(new FiltroPainel("Recebido", 1, 5), default);
        await repo.ListarAsync(new FiltroPainel("Processado' OR 1=1 --", 1, 5), default);
        Assert.Equal(banco.Captura.Comandos[1], banco.Captura.Comandos[3]);   // mesmo texto: o valor vai como parâmetro
        Assert.DoesNotContain("OR 1=1", banco.Captura.Comandos[3]);
    }

    [Fact]
    public async Task Filtro_em_campo_JSON_e_traduzido_para_o_banco()
    {
        await using var banco = await BancoEfemero.CriarAsync(9);
        await using var db = banco.Contexto();
        var app = await db.Pedidos.AsNoTracking().CountAsync(p => p.Metadados.Canal == "app");
        Assert.Equal(3, app);
        Assert.Contains("json_extract", banco.Captura.Comandos.Last(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Consulta_compilada_devolve_o_mesmo_resultado_da_comum()
    {
        await using var banco = await BancoEfemero.CriarAsync(3);
        await using var db = banco.Contexto();
        var id = await db.Pedidos.Select(p => p.Id).FirstAsync();
        var compilada = await new RepositorioPedidos(db, TimeProvider.System).ObterDetalheAsync(new PedidoId(id), default);
        var comum = await db.Pedidos.AsNoTracking().Where(p => p.Id == id).Select(p => new { p.Cliente, p.TotalCentavos, Itens = p.Itens.Count }).FirstAsync();
        Assert.Equal(comum.Cliente, compilada!.Cliente);
        Assert.Equal(comum.TotalCentavos / 100m, compilada.Total);
        Assert.Equal(comum.Itens, compilada.Itens.Count);
    }
}
