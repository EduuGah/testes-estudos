using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Filtros;
using PortalTarefas.Web.Seguranca;
using PortalTarefas.Web.Servicos;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.Controllers;

/// <summary>
/// Controller MVC da interface web. Mantém-se enxuto: recebe a requisição,
/// verifica o ModelState, chama o serviço e escolhe a view.
/// </summary>
[ServiceFilter(typeof(RegistroAcaoFilter))]   // filtro aplicado só neste controller (escopo justificado no relatório)
[Authorize(Policy = Politicas.LeituraPrivada)] // Unidade 6: páginas exigem login
public class TarefasController(ITarefaServico servico, IAuthorizationService autorizacao) : Controller
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
    [Authorize(Policy = Politicas.CriarTarefa)]
    public IActionResult Create() => View(new TarefaInputModel { Prazo = DateOnly.FromDateTime(DateTime.Today.AddDays(3)) });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.CriarTarefa)]
    public async Task<IActionResult> Create(TarefaInputModel modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(modelo);   // volta com mensagens e dados digitados

        var tarefa = new Tarefa
        {
            Equipe = User.FindFirstValue(TiposClaim.Equipe) ?? "Geral",
            ProprietarioId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };
        modelo.AplicarEm(tarefa);
        await servico.CriarAsync(tarefa, ct);
        TempData["Mensagem"] = $"Tarefa \"{tarefa.Titulo}\" cadastrada.";
        return RedirectToAction(nameof(Details), new { id = tarefa.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var tarefa = await servico.ObterAsync(id, ct);
        if (tarefa is null) return NaoEncontrada(id);
        if (!await PodeEditar(tarefa)) return Forbid();
        return View(TarefaInputModel.De(tarefa));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TarefaInputModel modelo, CancellationToken ct)
    {
        var atual = await servico.ObterAsync(id, ct);
        if (atual is null) return NaoEncontrada(id);
        if (!await PodeEditar(atual)) return Forbid();   // o servidor decide, mesmo que o botão esteja oculto
        if (!ModelState.IsValid) return View(modelo);

        var alteracoes = new Tarefa { Id = id };
        modelo.AplicarEm(alteracoes);
        try
        {
            var rowVersion = Convert.FromBase64String(modelo.RowVersion ?? "");
            if (!await servico.AtualizarAsync(alteracoes, rowVersion, ct)) return NaoEncontrada(id);
        }
        catch (ConflitoConcorrenciaException conflito)
        {
            // Não sobrescreve: mostra o que está gravado agora e entrega o token novo
            ModelState.AddModelError(string.Empty,
                "Esta tarefa foi alterada por outra pessoa depois que você abriu o formulário. " +
                $"Valores atuais: título \"{conflito.ValoresAtuais?.Titulo}\", situação {conflito.ValoresAtuais?.Situacao}. " +
                "Revise e salve novamente se quiser manter a sua versão.");
            if (conflito.ValoresAtuais is { } atuais)
            {
                ModelState.Remove(nameof(TarefaInputModel.RowVersion));
                modelo.RowVersion = Convert.ToBase64String(atuais.RowVersion);
            }
            return View(modelo);
        }
        TempData["Mensagem"] = "Alterações salvas.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /Tarefas/AlterarSituacao/3  (operação transacional com auditoria)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarSituacao(int id, string situacao, CancellationToken ct)
    {
        if (!Modelos.SituacaoTarefa.Todas.Contains(situacao)) return BadRequest();
        var tarefa = await servico.ObterAsync(id, ct);
        if (tarefa is null) return NaoEncontrada(id);
        if (!await PodeEditar(tarefa)) return Forbid();
        await servico.AlterarSituacaoAsync(id, situacao, ct);
        TempData["Mensagem"] = $"Situação alterada para {situacao}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<bool> PodeEditar(Tarefa tarefa) =>
        (await autorizacao.AuthorizeAsync(User, tarefa, Politicas.EditarTarefa)).Succeeded;

    private ViewResult NaoEncontrada(int id)
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NaoEncontrada", id);
    }
}
