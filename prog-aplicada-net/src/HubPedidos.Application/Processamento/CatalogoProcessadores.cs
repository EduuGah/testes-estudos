using System.Reflection;
using HubPedidos.Domain;

namespace HubPedidos.Application.Processamento;

/// <summary>
/// Catálogo construído uma única vez na inicialização, por reflexão restrita:
/// só o assembly informado, só o namespace informado e só tipos concretos.
/// </summary>
public sealed class CatalogoProcessadores
{
    private readonly Dictionary<CategoriaProcessamento, Type> _porCategoria;

    private CatalogoProcessadores(Dictionary<CategoriaProcessamento, Type> mapa) => _porCategoria = mapa;

    public IReadOnlyDictionary<CategoriaProcessamento, Type> Itens => _porCategoria;

    public static CatalogoProcessadores Construir(Assembly assembly, string prefixoNamespace)
    {
        var interfaceBase = typeof(IProcessadorPedido);
        var candidatos = assembly.GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith(prefixoNamespace, StringComparison.Ordinal))
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .OrderBy(t => t.FullName, StringComparer.Ordinal)   // ordem determinística
            .ToList();

        var registros = new List<(CategoriaProcessamento Categoria, int Versao, Type Tipo)>();
        foreach (var tipo in candidatos)
        {
            var atributos = tipo.GetCustomAttributes<ProcessadorPedidoAttribute>(inherit: false).ToList();
            var implementa = interfaceBase.IsAssignableFrom(tipo);

            if (atributos.Count > 0 && !implementa)
                throw new InvalidOperationException($"{tipo.FullName} tem [ProcessadorPedido] mas não implementa {interfaceBase.Name}.");
            if (implementa && atributos.Count == 0)
                throw new InvalidOperationException($"{tipo.FullName} implementa {interfaceBase.Name} sem declarar [ProcessadorPedido].");
            if (!implementa) continue;
            if (tipo.GetConstructor(Type.EmptyTypes) is null && tipo.GetConstructors().Length == 0)
                throw new InvalidOperationException($"{tipo.FullName} não tem construtor público.");

            foreach (var a in atributos)
            {
                if (a.Categoria == CategoriaProcessamento.Invalido || a.Versao < 1)
                    throw new InvalidOperationException($"{tipo.FullName}: metadado inválido ({a.Categoria}, v{a.Versao}).");
                registros.Add((a.Categoria, a.Versao, tipo));
            }
        }

        var duplicados = registros.GroupBy(r => (r.Categoria, r.Versao)).Where(g => g.Count() > 1).ToList();
        if (duplicados.Count > 0)
        {
            var d = duplicados[0];
            throw new InvalidOperationException(
                $"Processador duplicado para {d.Key.Categoria} v{d.Key.Versao}: {string.Join(", ", d.Select(x => x.Tipo.Name))}.");
        }

        // vale a maior versão de cada categoria
        var mapa = registros.GroupBy(r => r.Categoria)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Versao).First().Tipo);
        return new CatalogoProcessadores(mapa);
    }

    public Type TipoPara(CategoriaProcessamento categoria) =>
        _porCategoria.TryGetValue(categoria, out var t)
            ? t
            : throw new InvalidOperationException($"Não há processador para a categoria {categoria}.");
}
