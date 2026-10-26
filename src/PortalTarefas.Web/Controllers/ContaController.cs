using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Controllers;

public class EntrarViewModel
{
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha."), DataType(DataType.Password)]
    public string Senha { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

/// <summary>Login/logout das páginas MVC (mesmo cookie usado pela SPA).</summary>
[AllowAnonymous]
public class ContaController(SignInManager<UsuarioApp> signIn) : Controller
{
    [HttpGet]
    public IActionResult Entrar(string? returnUrl) => View(new EntrarViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(EntrarViewModel modelo)
    {
        if (!ModelState.IsValid) return View(modelo);
        var r = await signIn.PasswordSignInAsync(modelo.Email, modelo.Senha, false, lockoutOnFailure: true);
        if (!r.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");   // mensagem genérica
            modelo.Senha = string.Empty;
            return View(modelo);
        }
        // Url.IsLocalUrl evita redirecionamento aberto para sites externos
        return Url.IsLocalUrl(modelo.ReturnUrl) ? LocalRedirect(modelo.ReturnUrl) : RedirectToAction("Index", "Tarefas");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Sair()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Entrar));
    }

    public IActionResult AcessoNegado() => View();
}
