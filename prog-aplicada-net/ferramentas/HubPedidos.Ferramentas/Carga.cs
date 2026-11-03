using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HubPedidos.Application.Contratos;

namespace HubPedidos.Ferramentas;

/// <summary>
/// Teste de carga com concorrência fechada: N usuários virtuais repetem a rota sem pausa.
/// Aquecimento e janela de medição são separados; erros e timeouts entram na conta.
/// </summary>
public static class Carga
{
    public sealed record Resultado(string Rota, int Concorrencia, double JanelaSegundos, int Amostras, int Erros,
        double P50, double P95, double P99, double Max, double Media, double Vazao);

    public static async Task<int> ExecutarAsync(Opcoes o)
    {
        var url = o.Texto("url", "http://localhost:5080");
        var rota = o.Texto("rota", "painel");
        var aquecimento = o.Inteiro("aquecimento", 5);
        var duracao = o.Inteiro("duracao", 20);
        var degraus = o.Texto("degraus", o.Texto("concorrencia", "16")).Split(',').Select(int.Parse).ToArray();
        var saida = o.Texto("saida", $"carga-{rota}.json");

        var resultados = new List<Resultado>();
        foreach (var conc in degraus)
        {
            var r = await RodarAsync(url, rota, conc, aquecimento, duracao);
            resultados.Add(r);
            Console.WriteLine($"{r.Rota,-8} conc={r.Concorrencia,3}  amostras={r.Amostras,6}  erros={r.Erros,4}  p50={r.P50,7:F1} ms  p95={r.P95,7:F1} ms  p99={r.P99,7:F1} ms  máx={r.Max,7:F1} ms  vazão={r.Vazao,7:F1} req/s");
        }
        await File.WriteAllTextAsync(saida, JsonSerializer.Serialize(resultados, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static async Task<Resultado> RodarAsync(string url, string rota, int concorrencia, int aquecimento, int duracao)
    {
        using var http = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = concorrencia * 2 })
        {
            BaseAddress = new Uri(url),
            Timeout = TimeSpan.FromSeconds(5),
        };
        http.DefaultRequestHeaders.Add("X-Usuario", "carga");

        var amostras = new List<double>[concorrencia];
        var erros = new int[concorrencia];
        var relogio = Stopwatch.StartNew();
        var inicioMedicao = TimeSpan.FromSeconds(aquecimento);
        var fim = inicioMedicao + TimeSpan.FromSeconds(duracao);

        var usuarios = Enumerable.Range(0, concorrencia).Select(u => Task.Run(async () =>
        {
            amostras[u] = [];
            var rnd = new Random(u);
            while (relogio.Elapsed < fim)
            {
                var inicio = relogio.Elapsed;
                var ok = false;
                try
                {
                    using var resp = rota switch
                    {
                        "criar" => await http.PostAsJsonAsync("/api/v1/pedidos", new CriarPedidoRequest("Sudeste", "Normal",
                            [new ItemRequest("SKU-1", rnd.Next(1, 5), 19.90m), new ItemRequest("SKU-2", 1, 5m)])),
                        "detalhe" => await http.GetAsync("/api/v1/estatisticas"),
                        _ => await http.GetAsync($"/api/v1/pedidos?status=Processado&pagina={rnd.Next(1, 50)}&tamanho=20"),
                    };
                    ok = resp.IsSuccessStatusCode;
                    await resp.Content.LoadIntoBufferAsync();
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    ok = false;   // timeout ou falha de rede também é resultado
                }
                // só entram na amostra as requisições iniciadas dentro da janela de medição
                if (inicio >= inicioMedicao && relogio.Elapsed <= fim)
                {
                    if (ok) amostras[u].Add((relogio.Elapsed - inicio).TotalMilliseconds);
                    else erros[u]++;
                }
            }
        })).ToArray();
        await Task.WhenAll(usuarios);

        var todas = amostras.SelectMany(a => a).Order().ToArray();
        double P(double p) => todas.Length == 0 ? 0 : todas[Math.Clamp((int)Math.Ceiling(p * todas.Length) - 1, 0, todas.Length - 1)];
        return new Resultado(rota, concorrencia, duracao, todas.Length, erros.Sum(), P(0.50), P(0.95), P(0.99),
            todas.LastOrDefault(), todas.Length == 0 ? 0 : todas.Average(), (todas.Length + erros.Sum()) / (double)duracao);
    }
}
