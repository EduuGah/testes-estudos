using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Filtros;
using PortalTarefas.Web.Servicos;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.Controllers;

/// <summary>
/// Controller MVC da interface web. Mantém-se enxuto: recebe a requisição,
/// verifica o ModelState, chama o serviço e escolhe a view.
/// </summary>
[ServiceFilter(typeof(RegistroAcaoFilter))]   // filtro aplicado só neste controller (escopo justificado no relatório)
public class TarefasController(ITarefaServico servico) : Controller
{
    // GET /Tarefas?situacao=Pendente&busca=tela
    public async Task<IActionResult> Index(string? situacao, string? busca, CancellationToken ct)
    {
        var tarefas = (await servico.ListarAsync(situacao, busca, ct)).Select(TarefaResumoViewModel.De).ToList();
        return View(new ListaTarefasViewModel(tarefas, situacao, busca));
    }

    // GET /Tarefas/Details/2
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var tarefa = await servico.ObterAsync(id, ct);
        if (tarefa is null) return NaoEncontrada(id);
        return View(TarefaResumoViewModel.De(tarefa));
    }

    [HttpGet]
    public IActionResult Create() => View(new TarefaInputModel { Prazo = DateOnly.FromDateTime(DateTime.Today.AddDays(3)) });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TarefaInputModel modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(modelo);   // volta com mensagens e dados digitados

        var tarefa = new Tarefa();
        modelo.AplicarEm(tarefa);
        await servico.CriarAsync(tarefa, ct);
        TempData["Mensagem"] = $"Tarefa \"{tarefa.Titulo}\" cadastrada.";
        return RedirectToAction(nameof(Details), new { id = tarefa.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var tarefa = await servico.ObterAsync(id, ct);
        return tarefa is null ? NaoEncontrada(id) : View(TarefaInputModel.De(tarefa));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TarefaInputModel modelo, CancellationToken ct)
    {
        var tarefa = await servico.ObterAsync(id, ct);
        if (tarefa is null) return NaoEncontrada(id);
        if (!ModelState.IsValid) return View(modelo);

        modelo.AplicarEm(tarefa);
        await servico.AtualizarAsync(tarefa, ct);
        TempData["Mensagem"] = "Alterações salvas.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private ViewResult NaoEncontrada(int id)
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NaoEncontrada", id);
    }
}
