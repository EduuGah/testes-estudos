using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace HubPedidos.Application.Importacao;

/// <summary>Uma linha do lote: pedidoId;sku;quantidade;precoUnitario</summary>
public sealed record LinhaImportada(Guid PedidoId, string Sku, int Quantidade, decimal PrecoUnitario);

public sealed class LoteMuitoGrandeException(long limite) : Exception($"O lote ultrapassou o limite de {limite} bytes.");

public sealed class LinhaInvalidaException(int numero, string motivo) : Exception($"Linha {numero}: {motivo}")
{
    public int Numero { get; } = numero;
}

public interface IImportadorLote
{
    Task<IReadOnlyList<LinhaImportada>> ImportarAsync(Stream origem, long limiteBytes, CancellationToken ct);
}

/// <summary>Versão de referência: simples e correta, mas carrega o corpo inteiro e cria arrays/strings por campo.</summary>
public sealed class ImportadorBaseline : IImportadorLote
{
    public async Task<IReadOnlyList<LinhaImportada>> ImportarAsync(Stream origem, long limiteBytes, CancellationToken ct)
    {
        using var leitor = new StreamReader(origem, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var texto = await leitor.ReadToEndAsync(ct);
        if (Encoding.UTF8.GetByteCount(texto) > limiteBytes) throw new LoteMuitoGrandeException(limiteBytes);

        var resultado = new List<LinhaImportada>();
        var linhas = texto.Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            var linha = linhas[i].TrimEnd('\r');
            if (linha.Length == 0) continue;
            var campos = linha.Split(';');
            if (campos.Length != 4) throw new LinhaInvalidaException(i + 1, "esperados 4 campos");
            try
            {
                resultado.Add(new LinhaImportada(
                    Guid.Parse(campos[0]), campos[1],
                    int.Parse(campos[2], NumberStyles.Integer, CultureInfo.InvariantCulture),
                    decimal.Parse(campos[3], NumberStyles.Number, CultureInfo.InvariantCulture)));
            }
            catch (FormatException ex)
            {
                throw new LinhaInvalidaException(i + 1, ex.Message);
            }
        }
        return resultado;
    }
}

/// <summary>Lê linha a linha e recorta os campos com ReadOnlySpan&lt;char&gt;: sem Split e sem substrings intermediárias.</summary>
public sealed class ImportadorSpan : IImportadorLote
{
    public async Task<IReadOnlyList<LinhaImportada>> ImportarAsync(Stream origem, long limiteBytes, CancellationToken ct)
    {
        using var leitor = new StreamReader(origem, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 16 * 1024, leaveOpen: true);
        var resultado = new List<LinhaImportada>();
        long bytes = 0;
        var numero = 0;
        while (await leitor.ReadLineAsync(ct) is { } linha)
        {
            numero++;
            bytes += Encoding.UTF8.GetByteCount(linha) + 1;
            if (bytes - 1 > limiteBytes) throw new LoteMuitoGrandeException(limiteBytes);   // (bytes - 1) evita estouro quando o limite é long.MaxValue
            if (linha.Length == 0) continue;
            resultado.Add(Parse(linha, numero));   // o span vive só dentro de Parse: não atravessa await
        }
        return resultado;
    }

    private static LinhaImportada Parse(ReadOnlySpan<char> linha, int numero)
    {
        Span<Range> partes = stackalloc Range[5];
        if (linha.Split(partes, ';') != 4) throw new LinhaInvalidaException(numero, "esperados 4 campos");
        try
        {
            return new LinhaImportada(
                Guid.Parse(linha[partes[0]]),
                new string(linha[partes[1]]),
                int.Parse(linha[partes[2]], NumberStyles.Integer, CultureInfo.InvariantCulture),
                decimal.Parse(linha[partes[3]], NumberStyles.Number, CultureInfo.InvariantCulture));
        }
        catch (FormatException ex)
        {
            throw new LinhaInvalidaException(numero, ex.Message);
        }
    }
}

/// <summary>
/// Lê bytes UTF-8 direto do stream para um buffer alugado do ArrayPool, sem converter o lote em string.
/// O buffer sempre volta ao pool no finally, inclusive em erro ou cancelamento.
/// </summary>
public sealed class ImportadorUtf8Pooled(ArrayPool<byte>? pool = null, int tamanhoBuffer = 16 * 1024, bool limparAoDevolver = false) : IImportadorLote
{
    private readonly ArrayPool<byte> _pool = pool ?? ArrayPool<byte>.Shared;

    public async Task<IReadOnlyList<LinhaImportada>> ImportarAsync(Stream origem, long limiteBytes, CancellationToken ct)
    {
        var resultado = new List<LinhaImportada>();
        await ProcessarAsync(origem, limiteBytes, resultado.Add, ct);
        return resultado;
    }

    /// <summary>Núcleo em streaming: entrega cada linha ao consumidor sem guardar o lote inteiro.</summary>
    public async Task<long> ProcessarAsync(Stream origem, long limiteBytes, Action<LinhaImportada> consumidor, CancellationToken ct)
    {
        var buffer = _pool.Rent(tamanhoBuffer);
        var estado = new Estado();
        try
        {
            var usados = 0;
            long total = 0;
            while (true)
            {
                if (usados == buffer.Length)
                {
                    // linha maior que o buffer: troca por um maior e devolve o antigo
                    var maior = _pool.Rent(buffer.Length * 2);
                    buffer.AsSpan(0, usados).CopyTo(maior);
                    _pool.Return(buffer, limparAoDevolver);
                    buffer = maior;
                }

                var lidos = await origem.ReadAsync(buffer.AsMemory(usados), ct);
                if (lidos == 0) break;
                total += lidos;
                if (total > limiteBytes) throw new LoteMuitoGrandeException(limiteBytes);
                usados += lidos;

                var consumidos = ProcessarLinhas(buffer.AsSpan(0, usados), consumidor, estado, final: false);
                buffer.AsSpan(consumidos, usados - consumidos).CopyTo(buffer);   // sobra (linha incompleta) vai para o início
                usados -= consumidos;
            }
            if (usados > 0) ProcessarLinhas(buffer.AsSpan(0, usados), consumidor, estado, final: true);
            return estado.Linhas;
        }
        finally
        {
            _pool.Return(buffer, limparAoDevolver);
        }
    }

    private sealed class Estado
    {
        public int Numero;
        public long Linhas;
    }

    private static int ProcessarLinhas(ReadOnlySpan<byte> dados, Action<LinhaImportada> consumidor, Estado estado, bool final)
    {
        var consumidos = 0;
        while (true)
        {
            var resto = dados[consumidos..];
            var fim = resto.IndexOf((byte)'\n');
            if (fim < 0)
            {
                if (!final || resto.IsEmpty) return consumidos;
                fim = resto.Length;
            }
            var linha = resto[..fim];
            if (!linha.IsEmpty && linha[^1] == (byte)'\r') linha = linha[..^1];
            estado.Numero++;
            if (!linha.IsEmpty)
            {
                consumidor(Parse(linha, estado.Numero));
                estado.Linhas++;
            }
            consumidos += Math.Min(fim + 1, resto.Length);
            if (consumidos >= dados.Length) return consumidos;
        }
    }

    private static LinhaImportada Parse(ReadOnlySpan<byte> linha, int numero)
    {
        Span<Range> partes = stackalloc Range[5];
        if (Dividir(linha, partes) != 4) throw new LinhaInvalidaException(numero, "esperados 4 campos");

        var campoId = linha[partes[0]];
        var campoQtd = linha[partes[2]];
        var campoPreco = linha[partes[3]];
        if (!Utf8Parser.TryParse(campoId, out Guid id, out var n1) || n1 != campoId.Length)
            throw new LinhaInvalidaException(numero, "identificador inválido");
        if (!Utf8Parser.TryParse(campoQtd, out int quantidade, out var n2) || n2 != campoQtd.Length)
            throw new LinhaInvalidaException(numero, "quantidade inválida");
        if (!Utf8Parser.TryParse(campoPreco, out decimal preco, out var n3) || n3 != campoPreco.Length)
            throw new LinhaInvalidaException(numero, "preço inválido");

        // o SKU precisa virar string (é guardado); a decodificação UTF-8 trata caracteres multibyte
        return new LinhaImportada(id, Encoding.UTF8.GetString(linha[partes[1]]), quantidade, preco);
    }

    /// <summary>Divide a linha em até partes.Length campos, sem alocar.</summary>
    private static int Dividir(ReadOnlySpan<byte> linha, Span<Range> partes)
    {
        int quantidade = 0, inicio = 0;
        while (quantidade < partes.Length)
        {
            var pos = linha[inicio..].IndexOf((byte)';');
            if (pos < 0)
            {
                partes[quantidade++] = new Range(inicio, linha.Length);
                return quantidade;
            }
            partes[quantidade++] = new Range(inicio, inicio + pos);
            inicio += pos + 1;
        }
        return quantidade + 1;   // há mais campos do que o esperado
    }
}
