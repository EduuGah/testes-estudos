using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Servicos;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.ViewComponents;

/// <summary>View Component (extensão): bloco reutilizável com as tarefas vencidas, exibido no layout.</summary>
public class TarefasVencidasViewComponent(ITarefaServico servico) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var vencidas = (await servico.ListarAsync())
            .Select(TarefaResumoViewModel.De)
            .Where(t => t.Vencida)
            .ToList();
        return View(vencidas);
    }
}
