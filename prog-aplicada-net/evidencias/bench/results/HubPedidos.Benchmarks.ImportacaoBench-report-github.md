```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  Job-UWLSOM : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

IterationCount=10  LaunchCount=1  WarmupCount=3  

```
| Method              | Linhas | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2     | Allocated   | Alloc Ratio |
|-------------------- |------- |------------:|----------:|----------:|------:|--------:|----------:|----------:|---------:|------------:|------------:|
| **Baseline**            | **1000**   |    **265.0 μs** |   **4.87 μs** |   **2.90 μs** |  **1.00** |    **0.01** |   **66.4063** |   **33.2031** |  **33.2031** |   **679.34 KB** |        **1.00** |
| Span                | 1000   |          NA |        NA |        NA |     ? |       ? |        NA |        NA |       NA |          NA |           ? |
| Utf8Pooled          | 1000   |    106.8 μs |   1.07 μs |   0.71 μs |  0.40 |    0.00 |    7.2021 |    1.9531 |        - |   117.91 KB |        0.17 |
| Utf8PooledStreaming | 1000   |    104.5 μs |   0.74 μs |   0.44 μs |  0.39 |    0.00 |    6.1035 |         - |        - |   101.57 KB |        0.15 |
|                     |        |             |           |           |       |         |           |           |          |             |             |
| **Baseline**            | **50000**  | **25,879.0 μs** | **909.62 μs** | **601.66 μs** |  **1.00** |    **0.03** | **2312.5000** | **1937.5000** | **656.2500** | **33760.33 KB** |        **1.00** |
| Span                | 50000  |          NA |        NA |        NA |     ? |       ? |        NA |        NA |       NA |          NA |           ? |
| Utf8Pooled          | 50000  |  8,784.7 μs |  50.95 μs |  26.65 μs |  0.34 |    0.01 |  531.2500 |  515.6250 | 218.7500 |   6095.2 KB |        0.18 |
| Utf8PooledStreaming | 50000  |  5,255.3 μs |  74.32 μs |  49.16 μs |  0.20 |    0.00 |  304.6875 |         - |        - |  5070.48 KB |        0.15 |

Benchmarks with issues:
  ImportacaoBench.Span: Job-UWLSOM(IterationCount=10, LaunchCount=1, WarmupCount=3) [Linhas=1000]
  ImportacaoBench.Span: Job-UWLSOM(IterationCount=10, LaunchCount=1, WarmupCount=3) [Linhas=50000]
