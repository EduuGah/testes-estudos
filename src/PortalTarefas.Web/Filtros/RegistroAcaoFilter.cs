using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace PortalTarefas.Web.Filtros;

/// <summary>
/// Filtro de ação: registra nome da ação, se o modelo chegou válido, o tipo de
/// resultado e a duração. Ele apenas observa — não altera o resultado nem
/// substitui a validação, que continua sendo decidida pelo controller via ModelState.
/// </summary>
public class RegistroAcaoFilter(ILogger<RegistroAcaoFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate proximo)
    {
        var acao = $"{contexto.RouteData.Values["controller"]}.{contexto.RouteData.Values["action"]}";
        var cronometro = Stopwatch.StartNew();
        logger.LogInformation("[Filtro] Iniciando {Acao} | ModelState válido: {Valido}", acao, contexto.ModelState.IsValid);

        var executado = await proximo();

        cronometro.Stop();
        var resultado = executado.Exception is not null && !executado.ExceptionHandled
            ? "Exceção"
            : executado.Result switch
            {
                ViewResult v => $"View({v.ViewName ?? "padrão"})",
                RedirectToActionResult r => $"Redirect({r.ActionName})",
                NotFoundResult or NotFoundObjectResult => "404",
                IStatusCodeActionResult s => $"Status {s.StatusCode}",
                var outro => outro?.GetType().Name ?? "nenhum"
            };
        logger.LogInformation("[Filtro] Concluído {Acao} -> {Resultado} em {Duracao} ms", acao, resultado, cronometro.Elapsed.TotalMilliseconds.ToString("F1"));
    }
}
