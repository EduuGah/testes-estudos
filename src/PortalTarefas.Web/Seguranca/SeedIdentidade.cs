using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Seguranca;

/// <summary>
/// Cria roles e usuários de teste de forma controlada. A senha NÃO está no código:
/// vem de configuração externa (User Secrets "SeedIdentidade:Senha"). Sem ela, o seed de usuários é ignorado.
/// </summary>
public static class SeedIdentidade
{
    public static async Task AplicarAsync(IServiceProvider sp, IConfiguration config, ILogger logger)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var papel in new[] { Papeis.Usuario, Papeis.Administrador })
            if (!await roles.RoleExistsAsync(papel)) await roles.CreateAsync(new IdentityRole(papel));

        var senha = config["SeedIdentidade:Senha"];
        if (string.IsNullOrWhiteSpace(senha))
        {
            logger.LogWarning("SeedIdentidade:Senha não configurada; usuários de teste não foram criados.");
            return;
        }

        var usuarios = sp.GetRequiredService<UserManager<UsuarioApp>>();
        await Criar(usuarios, "ana@portal.local", "Ana (usuária – equipe Web)", senha, Papeis.Usuario, [new(TiposClaim.Equipe, "Web")]);
        await Criar(usuarios, "bruno@portal.local", "Bruno (usuário – equipe Dados)", senha, Papeis.Usuario, [new(TiposClaim.Equipe, "Dados")]);
        await Criar(usuarios, "carla@portal.local", "Carla (coordenação)", senha, Papeis.Usuario,
            [new(TiposClaim.Equipe, "Coordenacao"), new(TiposClaim.Permissao, TiposClaim.EditarOutrasEquipes)]);
        await Criar(usuarios, "admin@portal.local", "Administrador", senha, Papeis.Administrador, [new(TiposClaim.Equipe, "Geral")]);
    }

    private static async Task Criar(UserManager<UsuarioApp> um, string email, string nome, string senha, string papel, Claim[] claims)
    {
        if (await um.FindByEmailAsync(email) is not null) return;   // idempotente
        var usuario = new UsuarioApp { UserName = email, Email = email, EmailConfirmed = true, NomeExibicao = nome };
        var resultado = await um.CreateAsync(usuario, senha);
        if (!resultado.Succeeded)
            throw new InvalidOperationException("Falha ao criar usuário de teste: " + string.Join("; ", resultado.Errors.Select(e => e.Code)));
        await um.AddToRoleAsync(usuario, papel);
        await um.AddClaimsAsync(usuario, claims);
    }
}
