using HubPedidos.Api;
using HubPedidos.Api.Endpoints;
using HubPedidos.Api.TempoReal;
using HubPedidos.Application;
using HubPedidos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// valida o grafo de DI na inicialização: tempo de vida incompatível (dependência cativa) falha aqui
builder.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; });

// ---------------- Composition root: cada módulo registra o que é seu ----------------
builder.Services.AddModuloProcessamento(builder.Configuration);                      // Application
builder.Services.AddModuloPersistencia();                                            // Infrastructure
builder.Services.AddModuloApi();                                                     // API, tempo real e cache

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();   // depois de autenticação e autorização

app.MapOpenApi();
app.MapPedidos();
app.MapHub<PedidosHub>("/hubs/pedidos");
if (!app.Environment.IsProduction())
{
    // endpoint de laboratório: derruba as conexões SignalR para testar a reconexão
    app.MapPost("/dev/hub/derrubar", (RegistroConexoes r) => Results.Ok(new { derrubadas = r.AbortarTodas() }));
}

await ModuloPersistencia.PrepararBancoAsync(app.Services);
await app.RunAsync();
