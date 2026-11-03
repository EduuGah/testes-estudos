using System.Buffers;
using System.Text;
using HubPedidos.Application.Importacao;

namespace HubPedidos.Tests;

/// <summary>ArrayPool que conta aluguéis e devoluções para provar que nenhum buffer fica perdido.</summary>
public sealed class PoolContador : ArrayPool<byte>
{
    private int _alugados, _devolvidos, _maiorTamanho;
    public int Alugados => _alugados;
    public int Devolvidos => _devolvidos;
    public int MaiorTamanho => _maiorTamanho;

    public override byte[] Rent(int minimumLength)
    {
        Interlocked.Increment(ref _alugados);
        var b = Shared.Rent(minimumLength);
        _maiorTamanho = Math.Max(_maiorTamanho, b.Length);
        return b;
    }

    public override void Return(byte[] array, bool clearArray = false)
    {
        Interlocked.Increment(ref _devolvidos);
        Shared.Return(array, clearArray);
    }
}

/// <summary>Stream que gera linhas sob demanda: permite testar lotes grandes sem ter o lote inteiro na memória.</summary>
public sealed class StreamGerado(int linhas) : Stream
{
    private int _gerada;
    private byte[] _atual = [];
    private int _pos;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var escritos = 0;
        while (escritos < count)
        {
            if (_pos == _atual.Length)
            {
                if (_gerada == linhas) break;
                _atual = Encoding.UTF8.GetBytes($"{Guid.NewGuid()};SKU-{_gerada % 50};{_gerada % 7 + 1};{_gerada % 300}.25\n");
                _pos = 0;
                _gerada++;
            }
            var n = Math.Min(count - escritos, _atual.Length - _pos);
            Array.Copy(_atual, _pos, buffer, offset + escritos, n);
            _pos += n;
            escritos += n;
        }
        return escritos;
    }
}

public class ImportacaoTests
{
    private const long Limite = 10 * 1024 * 1024;
    private static Stream Texto(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    public static TheoryData<IImportadorLote> Importadores() =>
        [new ImportadorBaseline(), new ImportadorSpan(), new ImportadorUtf8Pooled(tamanhoBuffer: 64)];

    private static string Lote(int n) => string.Concat(Enumerable.Range(0, n).Select(i =>
        $"{new Guid(i, 0, 0, new byte[8])};SKU-{i};{i % 9 + 1};{i * 3}.{i % 100:00}\n"));

    [Fact]
    public async Task As_tres_versoes_sao_funcionalmente_equivalentes()
    {
        var lote = Lote(500) + "\r\n";   // inclui linha vazia com \r\n
        var esperado = await new ImportadorBaseline().ImportarAsync(Texto(lote), Limite, default);
        Assert.Equal(500, esperado.Count);
        Assert.Equal(esperado, await new ImportadorSpan().ImportarAsync(Texto(lote), Limite, default));
        Assert.Equal(esperado, await new ImportadorUtf8Pooled(tamanhoBuffer: 64).ImportarAsync(Texto(lote), Limite, default));
    }

    [Theory]
    [MemberData(nameof(Importadores))]
    public async Task Entrada_vazia_devolve_lista_vazia(IImportadorLote importador) =>
        Assert.Empty(await importador.ImportarAsync(Texto(""), Limite, default));

    [Theory]
    [MemberData(nameof(Importadores))]
    public async Task Linha_incompleta_informa_o_numero_da_linha(IImportadorLote importador)
    {
        var ex = await Assert.ThrowsAsync<LinhaInvalidaException>(() =>
            importador.ImportarAsync(Texto(Lote(2) + "c0ffee;SKU;1\n"), Limite, default));
        Assert.Equal(3, ex.Numero);
    }

    [Theory]
    [MemberData(nameof(Importadores))]
    public async Task Lote_acima_do_limite_e_recusado(IImportadorLote importador) =>
        await Assert.ThrowsAsync<LoteMuitoGrandeException>(() => importador.ImportarAsync(Texto(Lote(100)), 1_000, default));

    [Theory]
    [MemberData(nameof(Importadores))]
    public async Task Utf8_multibyte_e_preservado(IImportadorLote importador)
    {
        var linhas = await importador.ImportarAsync(Texto($"{Guid.NewGuid()};CAFÉ-Ç-日本;2;10.50\n"), Limite, default);
        Assert.Equal("CAFÉ-Ç-日本", linhas[0].Sku);
        Assert.Equal(10.50m, linhas[0].PrecoUnitario);
    }

    [Fact]
    public async Task Buffer_volta_ao_pool_mesmo_com_erro()
    {
        var pool = new PoolContador();
        await Assert.ThrowsAsync<LinhaInvalidaException>(() =>
            new ImportadorUtf8Pooled(pool, 64).ImportarAsync(Texto(Lote(20) + "lixo\n"), Limite, default));
        Assert.Equal(pool.Alugados, pool.Devolvidos);
    }

    [Fact]
    public async Task Buffer_volta_ao_pool_no_cancelamento()
    {
        var pool = new PoolContador();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ImportadorUtf8Pooled(pool, 64).ImportarAsync(new StreamGerado(1000), Limite, cts.Token));
        Assert.True(pool.Alugados > 0);
        Assert.Equal(pool.Alugados, pool.Devolvidos);
    }

    [Fact]
    public async Task Lote_grande_e_processado_em_streaming_com_buffer_pequeno()
    {
        var pool = new PoolContador();
        long linhas = 0;
        var total = await new ImportadorUtf8Pooled(pool, 16 * 1024).ProcessarAsync(
            new StreamGerado(200_000), long.MaxValue, _ => linhas++, default);   // ~14 MB de entrada
        Assert.Equal(200_000, total);
        Assert.Equal(200_000, linhas);
        Assert.True(pool.MaiorTamanho <= 32 * 1024, $"buffer máximo foi {pool.MaiorTamanho} bytes");
        Assert.Equal(pool.Alugados, pool.Devolvidos);
    }
}
