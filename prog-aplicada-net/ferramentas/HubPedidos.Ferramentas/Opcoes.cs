using System.Globalization;

namespace HubPedidos.Ferramentas;

public sealed class Opcoes(Dictionary<string, string> valores)
{
    public static Opcoes Ler(string[] args)
    {
        var d = new Dictionary<string, string>();
        for (var i = 0; i + 1 < args.Length; i += 2) d[args[i].TrimStart('-')] = args[i + 1];
        return new Opcoes(d);
    }

    public string Texto(string nome, string padrao) => valores.GetValueOrDefault(nome, padrao);
    public int Inteiro(string nome, int padrao) => valores.TryGetValue(nome, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : padrao;
}
