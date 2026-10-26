using Microsoft.AspNetCore.Identity;

namespace PortalTarefas.Web.Dominio;

/// <summary>Usuário do Portal: herda o que o Identity já gerencia (hash de senha, bloqueio, e-mail).</summary>
public class UsuarioApp : IdentityUser
{
    public string NomeExibicao { get; set; } = string.Empty;
}
