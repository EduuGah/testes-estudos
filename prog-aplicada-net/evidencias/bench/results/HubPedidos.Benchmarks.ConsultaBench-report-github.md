```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
INTEL XEON PLATINUM 8573C 3.10GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  Job-UWLSOM : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

IterationCount=10  LaunchCount=1  WarmupCount=3  

```
| Method    | Mean     | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|---------- |---------:|---------:|---------:|------:|-------:|----------:|------------:|
| Comum     | 82.42 μs | 1.320 μs | 0.873 μs |  1.00 | 0.2441 |  25.44 KB |        1.00 |
| Compilada | 45.94 μs | 0.092 μs | 0.061 μs |  0.56 | 0.0610 |    6.8 KB |        0.27 |
