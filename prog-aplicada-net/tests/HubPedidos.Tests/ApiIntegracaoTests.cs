using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HubPedidos.Api.Endpoints;
using HubPedidos.Application.Contratos;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace HubPedidos.Tests;

/// <summary>Cliente legado: representa o front-end que só conhece o contrato v1.</summary>
public sealed record ClienteV1Pedido(Guid Id, string Cliente, string Status, decimal Total, List<ClienteV1Item> Itens);
public sealed record ClienteV1Item(string Sku, int Quantidade, decimal PrecoUnitario);

public class ApiIntegracaoTests
{
    private static readonly CriarPedidoRequest PedidoValido =
        new("Sudeste", "Normal", [new ItemRequest("CAFE-01", 2, 19.90m), new ItemRequest("XICARA", 1, 35m)], Canal: "app");

    private static async Task<PedidoCriadoResponse> CriarAsync(HttpClient c, CriarPedidoRequest? r = null)
    {
        var resp = await c.PostAsJsonAsync("/api/v1/pedidos", r ?? PedidoValido);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<PedidoCriadoResponse>())!;
    }

    [Fact]
    public async Task Cria_e_consulta_o_pedido_processado_pela_fila()
    {
        await using var app = new HubApiFactory();
        var c = app.Cliente();
        var criado = await CriarAsync(c);
        Assert.Equal("Padrao", criado.Categoria);

        await app.Eventos.AguardarAsync(criado.Id, "Processado");   // espera o consumidor, sem Task.Delay arbitrário
        var v1 = await c.GetFromJsonAsync<ClienteV1Pedido>($"/api/v1/pedidos/{criado.Id}");
        Assert.Equal("Processado", v1!.Status);
        Assert.Equal(74.80m, v1.Total);
    }

    [Fact]
    public async Task Requisicao_invalida_recebe_400_com_os_campos()
    {
        await using var app = new HubApiFactory();
        var resp = await app.Cliente().PostAsJsonAsync("/api/v1/pedidos", new CriarPedidoRequest("Marte", null, []));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var corpo = await resp.Content.ReadAsStringAsync();
        Assert.Contains("regiao", corpo);
        Assert.Contains("itens", corpo);
    }

    [Fact]
    public async Task Sem_autenticacao_recebe_401_e_pedido_de_outro_usuario_404()
    {
        await using var app = new HubApiFactory();
        var criado = await CriarAsync(app.Cliente("ana"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Cliente(null).GetAsync($"/api/v1/pedidos/{criado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Cliente("bruno").GetAsync($"/api/v1/pedidos/{criado.Id}")).StatusCode);
    }

    [Fact]
    public async Task V1_e_v2_coexistem_e_o_cliente_v1_continua_funcionando()
    {
        await using var app = new HubApiFactory();
        var c = app.Cliente();
        var criado = await CriarAsync(c);

        var r1 = await c.GetAsync($"/api/v1/pedidos/{criado.Id}");
        var legado = await r1.Content.ReadFromJsonAsync<ClienteV1Pedido>();   // contrato v1 intacto
        Assert.Equal(2, legado!.Itens.Count);
        Assert.Equal(74.80m, legado.Total);
        Assert.True(r1.Headers.Contains("Deprecation"));
        Assert.Equal("1, 2", r1.Headers.GetValues("api-supported-versions").Single());

        var j2 = JsonDocument.Parse(await c.GetStringAsync($"/api/v2/pedidos/{criado.Id}")).RootElement;
        Assert.False(j2.TryGetProperty("status", out _));                    // mudança incompatível documentada
        Assert.Equal(74.80m, j2.GetProperty("valores").GetProperty("total").GetDecimal());
        Assert.Equal("BRL", j2.GetProperty("valores").GetProperty("moeda").GetString());
    }

    [Fact]
    public async Task Versao_ausente_usa_v1_e_versao_desconhecida_recebe_400()
    {
        await using var app = new HubApiFactory();
        var c = app.Cliente();
        var criado = await CriarAsync(c);

        var semVersao = await c.GetAsync($"/api/pedidos/{criado.Id}");
        Assert.Equal(HttpStatusCode.OK, semVersao.StatusCode);
        Assert.Equal("1", semVersao.Headers.GetValues("api-version").Single());

        var v9 = await c.GetAsync($"/api/v9/pedidos/{criado.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, v9.StatusCode);
        Assert.Contains("Versões suportadas: 1, 2", await v9.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ETag_estavel_304_sem_corpo_e_novo_ETag_quando_muda()
    {
        await using var app = new HubApiFactory();
        var c = app.Cliente();
        var criado = await CriarAsync(c);
        await app.Eventos.AguardarAsync(criado.Id, "Processado");

        var r1 = await c.GetAsync($"/api/v1/pedidos/{criado.Id}");
        var etag = r1.Headers.ETag!;
        var r2 = await c.GetAsync($"/api/v1/pedidos/{criado.Id}");
        Assert.Equal(etag, r2.Headers.ETag);                        // estável sem mudança

        var cond = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/pedidos/{criado.Id}");
        cond.Headers.IfNoneMatch.Add(etag);
        var r304 = await c.SendAsync(cond);
        Assert.Equal(HttpStatusCode.NotModified, r304.StatusCode);
        Assert.Empty(await r304.Content.ReadAsByteArrayAsync());

        var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/pedidos/{criado.Id}/status") { Content = JsonContent.Create(new AlterarStatusRequest("Cancelado")) };
        put.Headers.IfMatch.Add(etag);
        Assert.Equal(HttpStatusCode.OK, (await c.SendAsync(put)).StatusCode);

        var obsoleto = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/pedidos/{criado.Id}");
        obsoleto.Headers.IfNoneMatch.Add(etag);
        var r3 = await c.SendAsync(obsoleto);
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);              // ETag obsoleto: representação atual
        Assert.NotEqual(etag, r3.Headers.ETag);
    }

    [Fact]
    public async Task If_Match_obsoleto_recebe_412_e_ausente_428()
    {
        await using var app = new HubApiFactory();
        var c = app.Cliente();
        var criado = await CriarAsync(c);
        await app.Eventos.AguardarAsync(criado.Id, "Processado");

        var semIfMatch = await c.PutAsJsonAsync($"/api/v1/pedidos/{criado.Id}/status", new AlterarStatusRequest("Cancelado"));
        Assert.Equal((HttpStatusCode)428, semIfMatch.StatusCode);

        var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/pedidos/{criado.Id}/status") { Content = JsonContent.Create(new AlterarStatusRequest("Cancelado")) };
        put.Headers.TryAddWithoutValidation("If-Match", "\"etag-antigo\"");
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await c.SendAsync(put)).StatusCode);
    }

    [Fact]
    public async Task Output_cache_atende_leitura_publica_e_e_invalidado_por_tag()
    {
        await using var app = new HubApiFactory();
        var anonimo = app.Cliente(null);
        var contador = app.Services.GetRequiredService<ContadorEstatisticas>();

        await anonimo.GetAsync("/api/v1/estatisticas");
        await anonimo.GetAsync("/api/v1/estatisticas");
        Assert.Equal(1, contador.Execucoes);            // segunda leitura veio do cache

        var criado = await CriarAsync(app.Cliente("ana"));
        await app.Eventos.AguardarAsync(criado.Id, "Processado");   // a mudança invalida a tag "pedidos"
        await anonimo.GetAsync("/api/v1/estatisticas");
        Assert.Equal(2, contador.Execucoes);
    }

    [Fact]
    public async Task Respostas_autenticadas_nao_entram_no_cache()
    {
        await using var app = new HubApiFactory();
        var criado = await CriarAsync(app.Cliente("ana"));
        var r = await app.Cliente("ana").GetAsync($"/api/v1/pedidos/{criado.Id}");
        Assert.Equal("private, no-cache", r.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await app.Cliente("bruno").GetAsync($"/api/v1/pedidos/{criado.Id}")).StatusCode);
    }

    [Fact]
    public async Task SignalR_so_entrega_ao_dono_autorizado_no_servidor()
    {
        await using var app = new HubApiFactory();
        var criado = await CriarAsync(app.Cliente("ana"));
        await app.Eventos.AguardarAsync(criado.Id, "Processado");

        await using var ana = Conexao(app, "ana");
        await using var bruno = Conexao(app, "bruno");
        var recebidoAna = new TaskCompletionSource<PedidoAtualizadoEvento>();
        var recebidoBruno = 0;
        ana.On<PedidoAtualizadoEvento>("PedidoAtualizado", e => recebidoAna.TrySetResult(e));
        bruno.On<PedidoAtualizadoEvento>("PedidoAtualizado", _ => Interlocked.Increment(ref recebidoBruno));
        await ana.StartAsync();
        await bruno.StartAsync();

        await ana.InvokeAsync("AcompanharPedido", criado.Id);
        var negado = await Assert.ThrowsAsync<HubException>(() => bruno.InvokeAsync("AcompanharPedido", criado.Id));
        Assert.Contains("Acesso negado", negado.Message);

        var c = app.Cliente("ana");
        var etag = (await c.GetAsync($"/api/v1/pedidos/{criado.Id}")).Headers.ETag!;
        var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/pedidos/{criado.Id}/status") { Content = JsonContent.Create(new AlterarStatusRequest("Cancelado")) };
        put.Headers.IfMatch.Add(etag);
        await c.SendAsync(put);

        var evento = await recebidoAna.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Cancelado", evento.Status);
        Assert.Equal(0, recebidoBruno);
    }

    private static HubConnection Conexao(HubApiFactory app, string usuario) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(app.Server.BaseAddress, $"hubs/pedidos?usuario={usuario}"), o =>
            {
                o.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
            })
            .Build();

    [Fact]
    public async Task Grafo_de_DI_e_valido_e_ambiente_de_testes_recusa_banco_em_arquivo()
    {
        await using var app = new HubApiFactory();
        using var escopo = app.Services.CreateScope();
        Assert.NotNull(escopo.ServiceProvider.GetRequiredService<IPedidoRepositorio>());   // ValidateOnBuild já rodou na subida

        await using var perigosa = new HubApiFactory().WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Hub", "Data Source=hub.db"));
        var ex = Assert.ThrowsAny<Exception>(() => perigosa.CreateClient());
        Assert.Contains("SQLite em memória", ex.ToString());
    }
}
