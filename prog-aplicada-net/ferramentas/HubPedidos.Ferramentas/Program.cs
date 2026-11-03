using HubPedidos.Ferramentas;

// Ferramentas de laboratório usadas para gerar as evidências dos roteiros.
var comando = args.FirstOrDefault() ?? "ajuda";
var opcoes = Opcoes.Ler(args.Skip(1).ToArray());
return comando switch
{
    "catalogo" => Experimentos.Catalogo(),
    "sql" => await Experimentos.SqlAsync(),
    "fila" => await Experimentos.FilaAsync(),
    "semear" => await Experimentos.SemearAsync(opcoes.Texto("db", "hub.db"), opcoes.Inteiro("n", 20_000)),
    "carga" => await Carga.ExecutarAsync(opcoes),
    "tempo-real" => await TempoReal.ExecutarAsync(opcoes.Texto("url", "http://localhost:5080")),
    _ => Ajuda(),
};

static int Ajuda()
{
    Console.WriteLine("uso: ferramentas <catalogo|sql|fila|semear|carga|tempo-real> [--opcao valor]");
    return 1;
}
