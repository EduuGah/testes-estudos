using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(HubPedidos.Benchmarks.ImportacaoBench).Assembly).Run(args);
