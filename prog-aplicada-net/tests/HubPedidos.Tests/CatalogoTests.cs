using System.Reflection;
using HubPedidos.Application.Processamento;
using HubPedidos.Domain;

namespace HubPedidos.Tests;

public class CatalogoTests
{
    private static readonly Assembly Aplicacao = typeof(CatalogoProcessadores).Assembly;

    [Fact]
    public void Catalogo_descobre_somente_as_estrategias_do_namespace_previsto()
    {
        var c = CatalogoProcessadores.Construir(Aplicacao, "HubPedidos.Application.Processamento");
        Assert.Equal(5, c.Itens.Count);   // Padrao, Expresso, GrandeVolume, AltoValor, Internacional
        Assert.Equal(typeof(Estrategias.ProcessadorConferenciaManual), c.TipoPara(CategoriaProcessamento.AltoValor));
        Assert.Equal(typeof(Estrategias.ProcessadorConferenciaManual), c.TipoPara(CategoriaProcessamento.GrandeVolume));
        Assert.DoesNotContain(CategoriaProcessamento.Invalido, c.Itens.Keys);
    }

    [Fact]
    public void Catalogo_e_deterministico()
    {
        var a = CatalogoProcessadores.Construir(Aplicacao, "HubPedidos.Application.Processamento");
        var b = CatalogoProcessadores.Construir(Aplicacao, "HubPedidos.Application.Processamento");
        Assert.Equal(a.Itens.OrderBy(x => x.Key), b.Itens.OrderBy(x => x.Key));
    }

    [Fact]
    public void Duplicidade_falha_de_forma_legivel()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CatalogoProcessadores.Construir(typeof(CatalogoTests).Assembly, "HubPedidos.Tests.CatalogoDuplicado"));
        Assert.Contains("duplicado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Atributo_em_tipo_que_nao_implementa_a_interface_falha()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CatalogoProcessadores.Construir(typeof(CatalogoTests).Assembly, "HubPedidos.Tests.CatalogoInvalido"));
        Assert.Contains("não implementa", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Varredura_restrita_nao_encontra_tipos_de_outros_namespaces()
    {
        var c = CatalogoProcessadores.Construir(Aplicacao, "HubPedidos.Application.Importacao");
        Assert.Empty(c.Itens);
    }
}
