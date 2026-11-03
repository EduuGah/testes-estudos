```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763 2.79GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3
  Job-UWLSOM : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3

IterationCount=10  LaunchCount=1  WarmupCount=3  

```
| Method    | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|---------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
| Comum     | 157.94 μs | 0.406 μs | 0.241 μs |  1.00 | 1.4648 |  25.06 KB |        1.00 |
| Compilada |  76.36 μs | 0.146 μs | 0.097 μs |  0.48 | 0.3662 |    6.8 KB |        0.27 |
