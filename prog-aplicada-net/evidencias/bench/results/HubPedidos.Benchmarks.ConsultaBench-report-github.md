```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  Job-UWLSOM : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

IterationCount=10  LaunchCount=1  WarmupCount=3  

```
| Method    | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|---------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
| Comum     | 103.27 μs | 0.337 μs | 0.200 μs |  1.00 | 1.4648 |  25.36 KB |        1.00 |
| Compilada |  41.83 μs | 0.074 μs | 0.039 μs |  0.41 | 0.3662 |    6.8 KB |        0.27 |
