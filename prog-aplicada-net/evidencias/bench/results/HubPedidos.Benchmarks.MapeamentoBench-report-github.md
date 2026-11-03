```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763 3.18GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3
  Job-UWLSOM : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3

IterationCount=10  LaunchCount=1  WarmupCount=3  

```
| Method             | Mean     | Error     | StdDev    | Gen0   | Allocated |
|------------------- |---------:|----------:|----------:|-------:|----------:|
| MapearEClassificar | 1.139 μs | 0.0075 μs | 0.0050 μs | 0.0324 |     552 B |
