using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Seguranca;

namespace PortalTarefas.Web.Endpoints;

public record LoginDto(string Email, string Senha);
public record UsuarioAtualDto(string Id, string Email, string Nome, string[] Papeis, string? Equipe, bool PodeEditarOutrasEquipes);

/// <summary>Login, logout e sessão atual usando cookie do Identity (HttpOnly, Secure, SameSite=Strict).</summary>
public static class AutenticacaoEndpoints
{
    public static void MapAutenticacaoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth").WithTags("Autenticação");

        grupo.MapPost("/login", async Task<Results<Ok<UsuarioAtualDto>, ProblemHttpResult>> (
            LoginDto dto, SignInManager<UsuarioApp> signIn, UserManager<UsuarioApp> usuarios, ILoggerFactory logs) =>
        {
            var logger = logs.CreateLogger("Autenticacao");
            // lockoutOnFailure: true → após várias tentativas erradas a conta é bloqueada temporariamente
            var resultado = await signIn.PasswordSignInAsync(dto.Email, dto.Senha, isPersistent: false, lockoutOnFailure: true);
            if (!resultado.Succeeded)
            {
                // Mensagem única: não revela se o e-mail existe, se a senha errou ou se está bloqueado
                logger.LogWarning("Login recusado (bloqueado: {Bloqueado})", resultado.IsLockedOut);
                return TypedResults.Problem(statusCode: 401, title: "Falha no login", detail: "E-mail ou senha inválidos.");
            }
            var usuario = (await usuarios.FindByEmailAsync(dto.Email))!;
            logger.LogInformation("Login realizado para o usuário {UsuarioId}", usuario.Id);   // id, nunca senha
            var principal = await signIn.CreateUserPrincipalAsync(usuario);
            return TypedResults.Ok(ParaDto(principal, usuario.NomeExibicao));
        }).RequireRateLimiting("login");   // Unidade 7: limita tentativas de força bruta

        grupo.MapPost("/logout", async (SignInManager<UsuarioApp> signIn) =>
        {
            await signIn.SignOutAsync();   // remove o cookie; o security stamp invalida sessões antigas na revalidação
            return TypedResults.NoContent();
        }).RequireAuthorization();

        grupo.MapGet("/me", async Task<Results<Ok<UsuarioAtualDto>, UnauthorizedHttpResult>> (ClaimsPrincipal user, UserManager<UsuarioApp> usuarios) =>
        {
            var usuario = await usuarios.GetUserAsync(user);
            return usuario is null ? TypedResults.Unauthorized() : TypedResults.Ok(ParaDto(user, usuario.NomeExibicao));
        }).RequireAuthorization();
    }

    private static UsuarioAtualDto ParaDto(ClaimsPrincipal p, string nome) => new(
        p.FindFirstValue(ClaimTypes.NameIdentifier)!,
        p.FindFirstValue(ClaimTypes.Email) ?? p.Identity!.Name!,
        nome,
        p.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
        p.FindFirstValue(TiposClaim.Equipe),
        p.IsInRole(Papeis.Administrador) || p.HasClaim(TiposClaim.Permissao, TiposClaim.EditarOutrasEquipes));
}
