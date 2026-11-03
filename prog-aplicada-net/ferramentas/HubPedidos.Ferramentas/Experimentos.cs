using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Fila;
using HubPedidos.Application.Processamento;
using HubPedidos.Domain;
using HubPedidos.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HubPedidos.Ferramentas;

public static class Experimentos
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    private static Pedido Pedido(Regiao r, Prioridade p, params (int q, decimal v)[] itens) =>
        new(PedidoId.Novo(), "ana", r, p, [.. itens.Select((x, i) => new ItemPedido($"SKU-{i}", x.q, x.v))]);

    // ------------------------------------------------------------------ Roteiro 1
    public static int Catalogo()
    {
        var casos = new (string Caso, Pedido? Pedido)[]
        {
            ("Pedido nulo", null),
            ("Sem itens", Pedido(Regiao.Sul, Prioridade.Normal)),
            ("Normal: 2 x R$ 50,00", Pedido(Regiao.Sudeste, Prioridade.Normal, (2, 50m))),
            ("Prioridade alta", Pedido(Regiao.Sudeste, Prioridade.Alta, (1, 80m))),
            ("Exterior + prioridade alta", Pedido(Regiao.Exterior, Prioridade.Alta, (1, 80m))),
            ("Limite: R$ 9.999,99", Pedido(Regiao.Sul, Prioridade.Normal, (1, 9_999.99m))),
            ("Limite: R$ 10.000,00", Pedido(Regiao.Sul, Prioridade.Normal, (1, 10_000m))),
            ("Limite: 99 unidades", Pedido(Regiao.Norte, Prioridade.Normal, (99, 1m))),
            ("Limite: 100 unidades", Pedido(Regiao.Norte, Prioridade.Normal, (100, 1m))),
        };
        Console.WriteLine($"{"Caso",-30} {"Total",12}  Categoria");
        foreach (var (caso, p) in casos)
            Console.WriteLine($"{caso,-30} {(p?.Total ?? 0).ToString("N2", Br),12}  {ClassificadorPedidos.Classificar(p)}");

        var sw = Stopwatch.StartNew();
        var catalogo = CatalogoProcessadores.Construir(typeof(CatalogoProcessadores).Assembly, "HubPedidos.Application.Processamento");
        sw.Stop();
        Console.WriteLine();
        Console.WriteLine($"Catálogo construído por reflexão restrita em {sw.Elapsed.TotalMilliseconds:F2} ms (assembly HubPedidos.Application):");
        foreach (var (categoria, tipo) in catalogo.Itens.OrderBy(x => x.Key))
            Console.WriteLine($"  {categoria,-14} -> {tipo.Name}");
        return 0;
    }

    // ------------------------------------------------------------------ Roteiro 3
    public static async Task<int> SqlAsync()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var log = new List<string>();
        HubDbContext Contexto(bool logar) => new(new DbContextOptionsBuilder<HubDbContext>().UseSqlite(conexao)
            .LogTo(m => { if (logar) log.Add(m); }, [DbLoggerCategory.Database.Command.Name], Microsoft.Extensions.Logging.LogLevel.Information).Options);

        await using (var db = Contexto(false))
        {
            await db.Database.EnsureCreatedAsync();
            var repo = new RepositorioPedidos(db, TimeProvider.System);
            for (var i = 0; i < 20_000; i++)
            {
                db.Pedidos.Add(new PedidoEntidade
                {
                    Id = Guid.CreateVersion7(),
                    Cliente = i % 2 == 0 ? "ana" : "bruno",
                    Regiao = "Sudeste",
                    Prioridade = "Normal",
                    Status = i % 5 == 0 ? "Processado" : "Recebido",
                    TotalCentavos = 1_000 + i,
                    CriadoEmUtc = DateTime.UtcNow.AddSeconds(-i),
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Versao = 1,
                    Itens = [new ItemEntidade { Sku = "A", Quantidade = 1, PrecoCentavos = 1_000 + i }],
                    Metadados = new MetadadosEntidade { Canal = i % 3 == 0 ? "app" : "site", Tags = ["lote"] },
                });
                if (i % 2_000 == 1_999) { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
            }
        }

        async Task Bloco(string titulo, Func<HubDbContext, Task> acao)
        {
            log.Clear();
            await using var db = Contexto(true);
            await acao(db);
            Console.WriteLine($"==== {titulo} ({log.Count} comando(s))");
            foreach (var l in log) Console.WriteLine(Compactar(l));
            Console.WriteLine();
        }

        await Bloco("Painel: projeção + AsNoTracking + paginação", db =>
            new RepositorioPedidos(db, TimeProvider.System).ListarAsync(new FiltroPainel("Recebido", 3, 20), default));
        await Bloco("Atualização com tracking (só colunas alteradas + token de concorrência)", async db =>
        {
            var id = await db.Pedidos.AsNoTracking().OrderBy(p => p.CriadoEmUtc).Select(p => p.Id).FirstAsync();
            log.Clear();
            await new RepositorioPedidos(db, TimeProvider.System).AtualizarStatusAsync(new PedidoId(id), StatusPedido.Processado, 1, default);
        });
        await Bloco("Filtro em campo JSON (Metadados.Canal)", db => db.Pedidos.AsNoTracking().Where(p => p.Metadados.Canal == "app").CountAsync());
        await Bloco("Atualização de campo JSON", async db =>
        {
            var p = await db.Pedidos.OrderBy(x => x.CriadoEmUtc).FirstAsync();
            log.Clear();
            p.Metadados.Cupom = "PRIMEIRA";
            await db.SaveChangesAsync();
        });

        // N+1 x projeção: contagem de comandos
        log.Clear();
        await using (var db = Contexto(true)) await new RepositorioPedidos(db, TimeProvider.System).ListarIngenuoAsync(new FiltroPainel("Recebido", 3, 20), default);
        var ingenuo = log.Count;
        log.Clear();
        await using (var db = Contexto(true)) await new RepositorioPedidos(db, TimeProvider.System).ListarAsync(new FiltroPainel("Recebido", 3, 20), default);
        Console.WriteLine($"==== Comandos para a mesma página: ingênuo (N+1) = {ingenuo}, projeção = {log.Count}");
        Console.WriteLine();

        // paginação por deslocamento x por cursor (keyset) numa página profunda
        await using (var db = Contexto(false))
        {
            var corte = await db.Pedidos.AsNoTracking().OrderByDescending(p => p.CriadoEmUtc).ThenBy(p => p.Id).Skip(15_000).Select(p => new { p.CriadoEmUtc, p.Id }).FirstAsync();
            double Medir(Func<Task> f)
            {
                f().GetAwaiter().GetResult();
                var s = Stopwatch.StartNew();
                for (var i = 0; i < 50; i++) f().GetAwaiter().GetResult();
                return s.Elapsed.TotalMilliseconds / 50;
            }
            var offset = Medir(() => db.Pedidos.AsNoTracking().OrderByDescending(p => p.CriadoEmUtc).ThenBy(p => p.Id).Skip(15_000).Take(20).Select(p => p.Id).ToListAsync());
            var keyset = Medir(() => db.Pedidos.AsNoTracking().Where(p => p.CriadoEmUtc <= corte.CriadoEmUtc).OrderByDescending(p => p.CriadoEmUtc).ThenBy(p => p.Id).Take(20).Select(p => p.Id).ToListAsync());
            Console.WriteLine("==== Página profunda (posição 15.000 de 20.000), média de 50 execuções");
            Console.WriteLine($"  Deslocamento (Skip/Take): {offset:F2} ms");
            Console.WriteLine($"  Cursor (keyset/WHERE):    {keyset:F2} ms");
        }
        return 0;
    }

    private static string Compactar(string linha)
    {
        var i = linha.IndexOf("Executed DbCommand", StringComparison.Ordinal);
        var t = i >= 0 ? linha[i..] : linha;
        return string.Join(' ', t.Split((char[])['\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
    }

    // ------------------------------------------------------------------ Roteiro 4
    public static async Task<int> FilaAsync()
    {
        Console.WriteLine("Produtor: 2.000 pedidos a ~500/s. Banco simulado: 4 ms + 3 ms por operação concorrente extra (contenção).");
        Console.WriteLine();
        Console.WriteLine($"{"Configuração",-36} {"Aceitos",8} {"Rejeit.",8} {"Descart.",8} {"Process.",8} {"FilaMáx",8} {"Banco máx",9} {"p50 ms",8} {"p95 ms",8} {"Vazão/s",8}");
        foreach (var (nome, cap, cons, lim, modo) in new[]
        {
            ("A: cap 50, 4 consumidores, banco 4", 50, 4, 4, BoundedChannelFullMode.Wait),
            ("B: cap 500, 16 consumidores, banco 16", 500, 16, 16, BoundedChannelFullMode.Wait),
            ("C: cap 50, 4 cons., DropOldest", 50, 4, 4, BoundedChannelFullMode.DropOldest),
        })
        {
            var (s, maxBanco, vazao) = await RodarFilaAsync(cap, cons, lim, modo, 2_000, 500);
            Console.WriteLine($"{nome,-36} {s.Aceitos,8} {s.Rejeitados,8} {s.Descartados,8} {s.Processados,8} {s.ProfundidadeMaxima,8} {maxBanco,9} {s.LatenciaP50Ms,8:F0} {s.LatenciaP95Ms,8:F0} {vazao,8:F0}");
        }
        return 0;
    }

    private static async Task<(SnapshotFila, int, double)> RodarFilaAsync(int cap, int consumidores, int limiteBanco, BoundedChannelFullMode modo, int total, int porSegundo)
    {
        var opcoes = Options.Create(new OpcoesFila { Capacidade = cap, Consumidores = consumidores, MaxOperacoesBanco = limiteBanco, ModoCheio = modo });
        using var metricas = new MetricasFila();
        var fila = new FilaProcessamento(opcoes, metricas);
        var limitador = new LimitadorBanco(opcoes);
        var simultaneas = 0;

        async Task<int> BancoSimulado(CancellationToken ct)
        {
            var c = Interlocked.Increment(ref simultaneas);
            try { await Task.Delay(4 + 3 * (c - 1), ct); return c; }
            finally { Interlocked.Decrement(ref simultaneas); }
        }

        var relogio = Stopwatch.StartNew();
        var consumidoresTasks = Enumerable.Range(0, consumidores).Select(_ => Task.Run(async () =>
        {
            await foreach (var job in fila.Leitor.ReadAllAsync())
            {
                if (!metricas.IniciarProcessamento(job.PedidoId)) continue;
                await limitador.ExecutarAsync(BancoSimulado, default);
                metricas.Concluido(job.PedidoId, true, (DateTimeOffset.UtcNow - job.EnfileiradoEm).TotalMilliseconds);
            }
        })).ToArray();

        var porTick = porSegundo / 100;   // um tick a cada 10 ms
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        for (var enviados = 0; enviados < total;)
        {
            for (var i = 0; i < porTick && enviados < total; i++, enviados++)
                fila.TryEnfileirar(new ProcessarPedidoJob(PedidoId.Novo(), DateTimeOffset.UtcNow));
            await timer.WaitForNextTickAsync();
        }
        fila.Concluir();
        await Task.WhenAll(consumidoresTasks);
        var s = metricas.Snapshot(fila.Profundidade);
        return (s, limitador.MaximoObservado, s.Processados / relogio.Elapsed.TotalSeconds);
    }

    // ------------------------------------------------------------------ Roteiro 5 (massa de dados)
    public static async Task<int> SemearAsync(string arquivo, int n)
    {
        await using var db = new HubDbContext(new DbContextOptionsBuilder<HubDbContext>().UseSqlite($"Data Source={arquivo}").Options);
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
        var agora = DateTime.UtcNow;
        for (var i = 0; i < n; i++)
        {
            var itens = Enumerable.Range(0, i % 4 + 1).Select(k => new ItemEntidade { Sku = $"SKU-{k}", Quantidade = k + 1, PrecoCentavos = 990 + k * 100 }).ToList();
            db.Pedidos.Add(new PedidoEntidade
            {
                Id = Guid.CreateVersion7(),
                Cliente = $"cliente{i % 200}",
                Regiao = "Sudeste",
                Prioridade = "Normal",
                Status = (i % 10) switch { 0 => "Falhou", < 4 => "Recebido", _ => "Processado" },
                TotalCentavos = itens.Sum(x => x.Quantidade * x.PrecoCentavos),
                CriadoEmUtc = agora.AddSeconds(-i),
                AtualizadoEmUtc = agora,
                Versao = 1,
                Itens = itens,
                Metadados = new MetadadosEntidade { Canal = i % 3 == 0 ? "app" : "site" },
            });
            if (i % 2_000 == 1_999) { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"{n} pedidos gravados em {arquivo}");
        return 0;
    }
}
