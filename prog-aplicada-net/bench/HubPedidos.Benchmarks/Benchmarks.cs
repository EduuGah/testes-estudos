using System.Text;
using BenchmarkDotNet.Attributes;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Importacao;
using HubPedidos.Domain;
using HubPedidos.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HubPedidos.Benchmarks;

/// <summary>Roteiro 2: baseline x span x UTF-8 com pool, para lote pequeno e grande.</summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class ImportacaoBench
{
    private byte[] _dados = [];
    private readonly ImportadorBaseline _baseline = new();
    private readonly ImportadorSpan _span = new();
    private readonly ImportadorUtf8Pooled _pooled = new();

    [Params(1_000, 50_000)]
    public int Linhas { get; set; }

    [GlobalSetup]
    public void Preparar()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < Linhas; i++)
            sb.Append(Guid.NewGuid()).Append(";SKU-").Append(i % 500).Append(';').Append(i % 9 + 1).Append(';').Append(i % 1000).Append(".90\n");
        _dados = Encoding.UTF8.GetBytes(sb.ToString());
    }

    [Benchmark(Baseline = true)]
    public Task<IReadOnlyList<LinhaImportada>> Baseline() => _baseline.ImportarAsync(new MemoryStream(_dados), long.MaxValue, default);

    [Benchmark]
    public Task<IReadOnlyList<LinhaImportada>> Span() => _span.ImportarAsync(new MemoryStream(_dados), long.MaxValue, default);

    [Benchmark]
    public Task<IReadOnlyList<LinhaImportada>> Utf8Pooled() => _pooled.ImportarAsync(new MemoryStream(_dados), long.MaxValue, default);

    [Benchmark]
    public Task<long> Utf8PooledStreaming() => _pooled.ProcessarAsync(new MemoryStream(_dados), long.MaxValue, _ => { }, default);
}

/// <summary>Roteiro 3: consulta por id comum x compilada (SQLite em memória, 2.000 pedidos).</summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class ConsultaBench
{
    private SqliteConnection _conexao = null!;
    private HubDbContext _db = null!;
    private RepositorioPedidos _repo = null!;
    private Guid _id;

    [GlobalSetup]
    public async Task Preparar()
    {
        _conexao = new SqliteConnection("Data Source=:memory:");
        await _conexao.OpenAsync();
        _db = new HubDbContext(new DbContextOptionsBuilder<HubDbContext>().UseSqlite(_conexao).Options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new RepositorioPedidos(_db, TimeProvider.System);
        for (var i = 0; i < 2_000; i++)
        {
            var p = new Pedido(PedidoId.Novo(), "ana", Regiao.Sul, Prioridade.Normal, [new ItemPedido("A", 1, 10m), new ItemPedido("B", 2, 3m)]);
            await _repo.AdicionarAsync(p, new MetadadosPedido("app", null, []), default);
            if (i == 1_000) _id = p.Id.Valor;
        }
        _db.ChangeTracker.Clear();
    }

    [Benchmark(Baseline = true)]
    public Task<PedidoDetalhe?> Comum() =>
        _db.Pedidos.AsNoTracking().Where(p => p.Id == _id)
            .Select(p => new PedidoDetalhe(p.Id, p.Cliente, p.Regiao, p.Prioridade, p.Status, p.TotalCentavos / 100m, p.Versao, p.AtualizadoEmUtc,
                p.Itens.OrderBy(i => i.Id).Select(i => new ItemDetalhe(i.Sku, i.Quantidade, i.PrecoCentavos / 100m)).ToList(), p.Metadados.Canal))
            .FirstOrDefaultAsync();

    [Benchmark]
    public Task<PedidoDetalhe?> Compilada() => _repo.ObterDetalheAsync(new PedidoId(_id), default);

    [GlobalCleanup]
    public async Task Limpar()
    {
        await _db.DisposeAsync();
        await _conexao.DisposeAsync();
    }
}

/// <summary>Roteiro 5: método crítico isolável da rota de criação (validação + mapeamento DTO → domínio).</summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class MapeamentoBench
{
    private readonly CriarPedidoRequest _request = new("Sudeste", "Alta",
        [.. Enumerable.Range(0, 5).Select(i => new ItemRequest($"SKU-{i}", i + 1, 10m * i + 1))]);

    [Benchmark]
    public CategoriaProcessamento MapearEClassificar()
    {
        PedidoMapper.TryCriar(_request, "ana", out var pedido, out _);
        return ClassificadorPedidos.Classificar(pedido);
    }
}
