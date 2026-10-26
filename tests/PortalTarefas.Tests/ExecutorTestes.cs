using System.Diagnostics;
using System.Reflection;

namespace PortalTarefas.Tests;

/// <summary>Marca um método público assíncrono como caso de teste.</summary>
[AttributeUsage(AttributeTargets.Method)]
public class TesteAttribute(string descricao) : Attribute
{
    public string Descricao { get; } = descricao;
}

public static class Afirmar
{
    public static void Igual<T>(T esperado, T obtido, string? contexto = null)
    {
        if (!EqualityComparer<T>.Default.Equals(esperado, obtido))
            throw new Exception($"Esperado <{esperado}>, obtido <{obtido}>. {contexto}");
    }
    public static void Verdadeiro(bool condicao, string mensagem)
    {
        if (!condicao) throw new Exception(mensagem);
    }
}

/// <summary>
/// Executor simples: encontra métodos com [Teste], executa e imprime o resultado.
/// O código de saída é diferente de zero se algum teste falhar (usado pelo pipeline de CI).
/// </summary>
public static class ExecutorTestes
{
    public static async Task<int> ExecutarAsync(Assembly assembly, string? filtro)
    {
        var testes = assembly.GetTypes()
            .SelectMany(t => t.GetMethods().Select(m => (Tipo: t, Metodo: m, Attr: m.GetCustomAttribute<TesteAttribute>())))
            .Where(x => x.Attr is not null && (filtro is null || x.Tipo.Name.Contains(filtro)))
            .OrderBy(x => x.Tipo.Name).ThenBy(x => x.Metodo.MetadataToken)
            .ToList();

        int aprovados = 0, reprovados = 0;
        var total = Stopwatch.StartNew();
        Console.WriteLine($"Executando {testes.Count} teste(s)...\n");
        foreach (var (tipo, metodo, attr) in testes)
        {
            var instancia = Activator.CreateInstance(tipo);
            var sw = Stopwatch.StartNew();
            try
            {
                await (Task)metodo.Invoke(instancia, null)!;
                aprovados++;
                Console.WriteLine($"  [APROVADO] {tipo.Name}.{metodo.Name} - {attr!.Descricao} ({sw.ElapsedMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                reprovados++;
                var erro = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                Console.WriteLine($"  [FALHOU]   {tipo.Name}.{metodo.Name} - {attr!.Descricao}\n             {erro.Message}");
            }
            finally
            {
                (instancia as IDisposable)?.Dispose();
            }
        }
        Console.WriteLine($"\nResultado: {aprovados} aprovado(s), {reprovados} falha(s), total {testes.Count} em {total.Elapsed.TotalSeconds:F2} s");
        return reprovados == 0 ? 0 : 1;
    }
}
