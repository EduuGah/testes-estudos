using HubPedidos.Application.Contratos;
using HubPedidos.Domain;
using HubPedidos.Infrastructure;

namespace HubPedidos.Tests;

/// <summary>Teste arquitetural: a direção das dependências é verificada no build, não só no diagrama.</summary>
public class ArquiteturaTests
{
    private static string[] Referencias(Type t) => [.. t.Assembly.GetReferencedAssemblies().Select(a => a.Name!)];

    [Fact]
    public void Dominio_nao_depende_de_api_nem_de_ef_core()
    {
        var refs = Referencias(typeof(Pedido));
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, r => r.StartsWith("HubPedidos.", StringComparison.Ordinal));
    }

    [Fact]
    public void Aplicacao_nao_conhece_a_infraestrutura()
    {
        var refs = Referencias(typeof(IPedidoRepositorio));
        Assert.DoesNotContain("HubPedidos.Infrastructure", refs);
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Dtos_de_contrato_nao_dependem_de_tipos_de_infraestrutura()
    {
        var tiposInfra = typeof(HubDbContext).Assembly.GetTypes().ToHashSet();
        var contratos = typeof(IPedidoRepositorio).Assembly.GetTypes().Where(t => t.Namespace == "HubPedidos.Application.Contratos");
        foreach (var t in contratos)
            foreach (var p in t.GetProperties())
                Assert.DoesNotContain(p.PropertyType, tiposInfra);
    }
}
