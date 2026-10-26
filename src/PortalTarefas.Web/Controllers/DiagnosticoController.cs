using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Servicos;

namespace PortalTarefas.Web.Controllers;

/// <summary>
/// Versão MVC do endpoint de diagnóstico, equivalente ao /api/diagnostico da Minimal API.
/// </summary>
[ApiController]
[Route("api/mvc/diagnostico")]
public class DiagnosticoController(ITarefaServico servico, IWebHostEnvironment ambiente) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Get() => Ok(new
    {
        abordagem = "MVC (controller)",
        ambiente = ambiente.EnvironmentName,
        totalTarefas = await servico.ContarAsync(),
        horarioUtc = DateTime.UtcNow
    });
}
