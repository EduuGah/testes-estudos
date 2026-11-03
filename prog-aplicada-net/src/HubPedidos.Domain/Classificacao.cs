namespace HubPedidos.Domain;

public enum CategoriaProcessamento { Invalido, Internacional, AltoValor, Expresso, GrandeVolume, Padrao }

public static class ClassificadorPedidos
{
    public const decimal LimiteAltoValor = 10_000m;
    public const int LimiteGrandeVolume = 100;

    /// <summary>
    /// A ordem dos braços é a regra de negócio: inválido primeiro, depois exterior, valor, prioridade e volume.
    /// O descarte final (_) torna a expressão exaustiva para qualquer combinação nova.
    /// </summary>
    public static CategoriaProcessamento Classificar(Pedido? pedido) => pedido switch
    {
        null => CategoriaProcessamento.Invalido,
        { Itens.IsEmpty: true } => CategoriaProcessamento.Invalido,
        { Total: <= 0m } => CategoriaProcessamento.Invalido,
        { Regiao: Regiao.Exterior } => CategoriaProcessamento.Internacional,
        { Total: >= LimiteAltoValor } => CategoriaProcessamento.AltoValor,
        { Prioridade: Prioridade.Alta, Itens: [_, ..] } => CategoriaProcessamento.Expresso,
        { QuantidadeTotal: >= LimiteGrandeVolume } or { Itens.Length: > 20 } => CategoriaProcessamento.GrandeVolume,
        _ => CategoriaProcessamento.Padrao,
    };
}
