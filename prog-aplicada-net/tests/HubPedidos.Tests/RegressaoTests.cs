using System.Text;
using HubPedidos.Application.Importacao;

namespace HubPedidos.Tests;

public class RegressaoTests
{
    /// <summary>Erro encontrado pelo benchmark: com limite long.MaxValue, "limite + 1" estourava e todo lote era recusado.</summary>
    [Fact]
    public async Task ImportadorSpan_aceita_limite_maximo()
    {
        var linha = $"{Guid.NewGuid()};SKU;1;2.50\n";
        var r = await new ImportadorSpan().ImportarAsync(new MemoryStream(Encoding.UTF8.GetBytes(linha)), long.MaxValue, default);
        Assert.Single(r);
    }
}
