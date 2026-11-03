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
| Comum     | 104.46 μs | 0.579 μs | 0.383 μs |  1.00 | 1.4648 |  25.64 KB |        1.00 |
| Compilada |  42.14 μs | 0.072 μs | 0.043 μs |  0.40 | 0.3662 |    6.8 KB |        0.27 |
