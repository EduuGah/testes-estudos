using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Seguranca;

public static class Papeis
{
    public const string Usuario = "Usuario";
    public const string Administrador = "Administrador";
}

public static class TiposClaim
{
    public const string Equipe = "equipe";
    public const string Permissao = "permissao";
    public const string EditarOutrasEquipes = "editar-outras-equipes";
}

public static class Politicas
{
    public const string LeituraPrivada = "LeituraPrivada";       // qualquer usuário autenticado
    public const string CriarTarefa = "CriarTarefa";             // role Usuario ou Administrador
    public const string EditarTarefa = "EditarTarefa";           // regra por recurso (equipe/proprietário)
    public const string EditarOutraEquipe = "EditarOutraEquipe"; // baseada em claim
    public const string ExcluirTarefa = "ExcluirTarefa";         // somente Administrador

    public static void Registrar(AuthorizationOptions o)
    {
        o.AddPolicy(LeituraPrivada, p => p.RequireAuthenticatedUser());
        o.AddPolicy(CriarTarefa, p => p.RequireRole(Papeis.Usuario, Papeis.Administrador));
        o.AddPolicy(EditarOutraEquipe, p => p.RequireClaim(TiposClaim.Permissao, TiposClaim.EditarOutrasEquipes));
        o.AddPolicy(EditarTarefa, p => p.RequireAuthenticatedUser().AddRequirements(new EditarTarefaRequirement()));
        o.AddPolicy(ExcluirTarefa, p => p.RequireRole(Papeis.Administrador));
    }
}

/// <summary>Requisito avaliado contra a própria tarefa (autorização baseada em recurso).</summary>
public class EditarTarefaRequirement : IAuthorizationRequirement;

/// <summary>
/// Pode editar quem: é Administrador, OU é o proprietário, OU é da mesma equipe,
/// OU possui a claim permissao=editar-outras-equipes.
/// </summary>
public class EditarTarefaHandler : AuthorizationHandler<EditarTarefaRequirement, Tarefa>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext contexto, EditarTarefaRequirement requisito, Tarefa tarefa)
    {
        var usuario = contexto.User;
        var mesmaEquipe = usuario.HasClaim(TiposClaim.Equipe, tarefa.Equipe);
        var proprietario = tarefa.ProprietarioId is not null && tarefa.ProprietarioId == usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        var permissaoAmpla = usuario.HasClaim(TiposClaim.Permissao, TiposClaim.EditarOutrasEquipes);

        if (usuario.IsInRole(Papeis.Administrador) || proprietario || mesmaEquipe || permissaoAmpla)
            contexto.Succeed(requisito);
        return Task.CompletedTask;
    }
}
