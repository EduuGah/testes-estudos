using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HubPedidos.Api.Seguranca;

/// <summary>
/// Autenticação DIDÁTICA: identifica o usuário pelo cabeçalho X-Usuario (ou ?usuario= no SignalR).
/// Serve para demonstrar autorização por dono do pedido; em produção seria JWT/cookie de um provedor real.
/// </summary>
public sealed class AutenticacaoCabecalho(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e)
    : AuthenticationHandler<AuthenticationSchemeOptions>(o, l, e)
{
    public const string Esquema = "Cabecalho";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var usuario = Request.Headers["X-Usuario"].ToString();
        if (string.IsNullOrWhiteSpace(usuario)) usuario = Request.Query["usuario"].ToString();
        if (string.IsNullOrWhiteSpace(usuario)) return Task.FromResult(AuthenticateResult.NoResult());
        if (usuario.Length > 40 || !usuario.All(char.IsLetterOrDigit))
            return Task.FromResult(AuthenticateResult.Fail("Usuário inválido."));

        var identidade = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, usuario), new Claim(ClaimTypes.Name, usuario)], Esquema);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema)));
    }
}
