using System.Collections.Immutable;
using HubPedidos.Application.Contratos;
using HubPedidos.Domain;

namespace HubPedidos.Tests;

public class DominioTests
{
    private static Pedido Criar(Regiao regiao = Regiao.Sudeste, Prioridade prioridade = Prioridade.Normal, params (int qtd, decimal preco)[] itens) =>
        new(PedidoId.Novo(), "ana", regiao, prioridade, [.. itens.Select((x, i) => new ItemPedido($"SKU-{i}", x.qtd, x.preco))]);

    public static TheoryData<string, Pedido?, CategoriaProcessamento> Casos() => new()
    {
        { "nulo", null, CategoriaProcessamento.Invalido },
        { "sem itens", Criar(), CategoriaProcessamento.Invalido },
        { "total zero", Criar(itens: (1, 0m)), CategoriaProcessamento.Invalido },
        { "normal", Criar(itens: (2, 50m)), CategoriaProcessamento.Padrao },
        { "prioritário", Criar(prioridade: Prioridade.Alta, itens: (1, 80m)), CategoriaProcessamento.Expresso },
        { "exterior vence prioridade", Criar(Regiao.Exterior, Prioridade.Alta, (1, 80m)), CategoriaProcessamento.Internacional },
        { "limite: 9.999,99 não é alto valor", Criar(itens: (1, 9_999.99m)), CategoriaProcessamento.Padrao },
        { "limite: 10.000,00 é alto valor", Criar(itens: (1, 10_000m)), CategoriaProcessamento.AltoValor },
        { "limite: 99 unidades", Criar(itens: (99, 1m)), CategoriaProcessamento.Padrao },
        { "limite: 100 unidades", Criar(itens: (100, 1m)), CategoriaProcessamento.GrandeVolume },
        { "21 itens distintos", Criar(itens: [.. Enumerable.Repeat((1, 1m), 21)]), CategoriaProcessamento.GrandeVolume },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void Classifica_cada_classe_de_equivalencia(string caso, Pedido? pedido, CategoriaProcessamento esperado)
    {
        Assert.False(string.IsNullOrEmpty(caso));
        Assert.Equal(esperado, ClassificadorPedidos.Classificar(pedido));
    }

    [Fact]
    public void With_cria_copia_sem_alterar_o_original()
    {
        var original = new ItemPedido("CAFE", 2, 10m);
        var copia = original with { Quantidade = 5 };
        Assert.Equal(2, original.Quantidade);
        Assert.Equal(5, copia.Quantidade);
        Assert.Equal(new ItemPedido("CAFE", 2, 10m), original);   // igualdade por valor
    }

    [Fact]
    public void Record_com_colecao_compara_a_referencia_do_array()
    {
        // imutabilidade superficial x igualdade: dois pedidos com os mesmos itens, arrays diferentes
        var id = PedidoId.Novo();
        var a = new Pedido(id, "ana", Regiao.Sul, Prioridade.Normal, [new ItemPedido("X", 1, 1m)]);
        var b = new Pedido(id, "ana", Regiao.Sul, Prioridade.Normal, [new ItemPedido("X", 1, 1m)]);
        Assert.NotEqual(a, b);
        Assert.Equal(a, a with { });   // a cópia reaproveita o mesmo ImmutableArray
    }

    [Fact]
    public void Item_invalido_nao_e_criado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemPedido("X", 0, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemPedido("X", 1, -1m));
    }

    [Theory]
    [InlineData("0199f0c2-6c5b-7c4e-9e8b-2b3a4c5d6e7f", true)]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData("abc", false)]
    public void PedidoId_implementa_IParsable(string texto, bool valido)
    {
        Assert.Equal(valido, PedidoId.TryParse(texto, null, out _));
    }

    [Fact]
    public void Dto_invalido_e_rejeitado_antes_de_criar_o_dominio()
    {
        var request = new CriarPedidoRequest("Marte", "Urgentissima", [new ItemRequest("", 0, -5m)]);
        var ok = PedidoMapper.TryCriar(request, "ana", out var pedido, out var erros);
        Assert.False(ok);
        Assert.Null(pedido);
        Assert.Contains("regiao", erros.Keys);
        Assert.Contains("prioridade", erros.Keys);
        Assert.Contains("itens[0].sku", erros.Keys);
        Assert.Contains("itens[0].quantidade", erros.Keys);
        Assert.Contains("itens[0].precoUnitario", erros.Keys);
    }

    [Fact]
    public void Dto_valido_gera_dominio_com_colecao_imutavel()
    {
        var itens = new List<ItemRequest> { new("A", 1, 10m) };
        Assert.True(PedidoMapper.TryCriar(new CriarPedidoRequest("sudeste", null, itens), "ana", out var pedido, out _));
        itens.Add(new ItemRequest("B", 1, 1m));   // mudar a lista do DTO não afeta o domínio
        Assert.Single(pedido!.Itens);
        Assert.IsType<ImmutableArray<ItemPedido>>(pedido.Itens);
    }
}
