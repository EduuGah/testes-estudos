namespace PortalTarefas.Web.Integracoes;

/// <summary>
/// Simulador local da dependência externa (somente Development), para reproduzir
/// falhas transitórias, lentidão e indisponibilidade sem depender de um serviço real.
///   POST /simulador/dicas/modo/{normal|transitorio|fora|lento}
/// </summary>
public static class SimuladorDicas
{
    private static string _modo = "normal";
    private static int _contador;

    public static void MapSimuladorDicas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/simulador/dicas").ExcludeFromDescription();

        g.MapPost("/modo/{modo}", (string modo) => { _modo = modo; _contador = 0; return Results.Ok(new { modo }); });

        g.MapGet("/dicas/aleatoria", async (CancellationToken ct) =>
        {
            var n = Interlocked.Increment(ref _contador);
            switch (_modo)
            {
                case "transitorio" when n % 3 != 0:   // falha 2 de cada 3 chamadas → o retry resolve
                    return Results.StatusCode(503);
                case "fora":                          // sempre falha → circuito abre
                    return Results.StatusCode(503);
                case "lento":                         // excede o timeout por tentativa
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    break;
            }
            return Results.Ok(new DicaDto($"Revise as tarefas vencidas antes de criar novas (#{n}).", "servico-dicas"));
        });

        g.MapGet("/saude", () => _modo == "fora" ? Results.StatusCode(503) : Results.Ok());
    }
}
