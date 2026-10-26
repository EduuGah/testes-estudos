using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Servicos;

namespace PortalTarefas.Web.Controllers;

/// <summary>
/// Versão MVC do endpoint de diagnóstico, equivalente ao /api/diagnostico da Minimal API.
/// </summary>
[ApiController]
[Route("api/mvc/diagnostico")]
public class DiagnosticoController(RepositorioTarefasMemoria repositorio, IWebHostEnvironment ambiente) : ControllerBase
{
    [HttpGet]
    public ActionResult<object> Get() => Ok(new
    {
        abordagem = "MVC (controller)",
        ambiente = ambiente.EnvironmentName,
        totalTarefas = repositorio.Total,
        horarioUtc = DateTime.UtcNow
    });
}
