using System.Reflection;
using HubPedidos.Application.Fila;
using HubPedidos.Application.Importacao;
using HubPedidos.Application.Processamento;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HubPedidos.Application;

/// <summary>Registro do módulo de processamento, chamado pelo composition root (Program.cs).</summary>
public static class ModuloProcessamento
{
    public static IServiceCollection AddModuloProcessamento(this IServiceCollection services, IConfiguration configuracao)
    {
        services.Configure<OpcoesFila>(configuracao.GetSection("Fila"));

        // Singleton: estado compartilhado e sem dependências scoped
        services.AddSingleton<MetricasFila>();
        services.AddSingleton<FilaProcessamento>();
        services.AddSingleton<LimitadorBanco>();

        // Catálogo: reflexão uma única vez, restrita a este assembly e namespace
        var catalogo = CatalogoProcessadores.Construir(Assembly.GetExecutingAssembly(), "HubPedidos.Application.Processamento");
        services.AddSingleton(catalogo);
        foreach (var tipo in catalogo.Itens.Values.Distinct())
            services.AddScoped(tipo);

        // Scoped: dependem do repositório (DbContext scoped)
        services.AddScoped<SeletorProcessador>();
        services.AddScoped<ServicoProcessamento>();
        services.AddSingleton<ImportadorUtf8Pooled>();

        services.AddHostedService<WorkerProcessamento>();
        return services;
    }
}
