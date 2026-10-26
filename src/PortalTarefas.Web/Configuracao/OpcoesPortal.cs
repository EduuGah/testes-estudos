using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.Configuracao;

/// <summary>Seção "Portal": valores não sensíveis que mudam por ambiente.</summary>
public class OpcoesPortal
{
    public const string Secao = "Portal";

    [Required] public string NomeAmbiente { get; set; } = string.Empty;

    /// <summary>Origens autorizadas a chamar a API pelo navegador (CORS). Nunca "*".</summary>
    [MinLength(1)] public string[] OrigensPermitidas { get; set; } = [];
}

/// <summary>Seção "ServicoDicas": endereço da dependência HTTP.</summary>
public class OpcoesServicoDicas
{
    public const string Secao = "ServicoDicas";

    [Required, Url] public string UrlBase { get; set; } = string.Empty;
}
