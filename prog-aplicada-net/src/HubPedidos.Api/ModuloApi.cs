using System.Text.Json.Serialization;
using HubPedidos.Api.Endpoints;
using HubPedidos.Api.Seguranca;
using HubPedidos.Api.TempoReal;
using HubPedidos.Application.Contratos;
using Microsoft.AspNetCore.Authentication;

namespace HubPedidos.Api;

public static class ModuloApi
{
    public static IServiceCollection AddModuloApi(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddAuthentication(AutenticacaoCabecalho.Esquema)
            .AddScheme<AuthenticationSchemeOptions, AutenticacaoCabecalho>(AutenticacaoCabecalho.Esquema, null);
        services.AddAuthorization();
        services.AddSignalR();
        services.AddSingleton<RegistroConexoes>();
        services.AddSingleton<INotificadorPedidos, NotificadorApi>();
        services.AddSingleton<ContadorEstatisticas>();
        services.AddOutputCache(o =>
        {
            // por padrão o output cache já ignora requisições autenticadas; a política abaixo vale só para a rota pública
            o.AddPolicy("Estatisticas", p => p.Expire(TimeSpan.FromSeconds(30)).Tag(NotificadorApi.TagCache).SetVaryByQuery("*"));
        });
        services.AddOpenApi("v1");
        services.AddOpenApi("v2");
        return services;
    }
}
