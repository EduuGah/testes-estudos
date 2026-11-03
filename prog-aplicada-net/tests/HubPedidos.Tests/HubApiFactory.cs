using System.Collections.Concurrent;
using System.Net.Http.Headers;
using HubPedidos.Api.TempoReal;
using HubPedidos.Application.Contratos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HubPedidos.Tests;

/// <summary>
/// Sobe a API real em memória: ambiente "Testes", banco SQLite em memória exclusivo desta instância
/// e o notificador decorado para que os testes aguardem eventos sem atrasos arbitrários.
/// </summary>
public sealed class HubApiFactory : WebApplicationFactory<Program>
{
    private readonly string _banco = $"Data Source=hub-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _mantemVivo;

    public HubApiFactory()
    {
        _mantemVivo = new SqliteConnection(_banco);
        _mantemVivo.Open();
    }

    public NotificadorObservavel Eventos => Services.GetRequiredService<NotificadorObservavel>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testes");
        builder.UseSetting("ConnectionStrings:Hub", _banco);
        builder.UseSetting("Fila:Capacidade", "50");
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<INotificadorPedidos>();
            s.AddSingleton<NotificadorApi>();
            s.AddSingleton<NotificadorObservavel>();
            s.AddSingleton<INotificadorPedidos>(sp => sp.GetRequiredService<NotificadorObservavel>());
        });
    }

    public HttpClient Cliente(string? usuario = "ana")
    {
        var c = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (usuario is not null) c.DefaultRequestHeaders.Add("X-Usuario", usuario);
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return c;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _mantemVivo.Dispose();
    }
}

/// <summary>Decorador: repassa ao notificador real e permite aguardar um status específico.</summary>
public sealed class NotificadorObservavel(NotificadorApi real) : INotificadorPedidos
{
    private readonly ConcurrentDictionary<(Guid, string), TaskCompletionSource<PedidoAtualizadoEvento>> _esperas = new();

    public async Task PedidoAtualizadoAsync(PedidoAtualizadoEvento evento, CancellationToken ct)
    {
        await real.PedidoAtualizadoAsync(evento, ct);
        Espera(evento.PedidoId, evento.Status).TrySetResult(evento);
    }

    public Task<PedidoAtualizadoEvento> AguardarAsync(Guid id, string status) =>
        Espera(id, status).Task.WaitAsync(TimeSpan.FromSeconds(10));

    private TaskCompletionSource<PedidoAtualizadoEvento> Espera(Guid id, string status) =>
        _esperas.GetOrAdd((id, status), _ => new TaskCompletionSource<PedidoAtualizadoEvento>(TaskCreationOptions.RunContinuationsAsynchronously));
}
