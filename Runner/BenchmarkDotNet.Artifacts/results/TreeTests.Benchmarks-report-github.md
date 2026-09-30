```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
11th Gen Intel Core i5-11400H 2.70GHz (Max: 2.69GHz), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4


```
| Method  | Mean         | Error       | StdDev      | Ratio    | RatioSD | Allocated | Alloc Ratio |
|-------- |-------------:|------------:|------------:|---------:|--------:|----------:|------------:|
| IkdTree |     208.3 μs |     4.08 μs |     5.45 μs |     1.00 |    0.04 |         - |          NA |
| CkdTree | 291,852.3 μs | 5,647.83 μs | 4,716.19 μs | 1,402.19 |   41.53 |         - |          NA |
| K2Tree  |  25,225.1 μs |   518.80 μs | 1,437.58 μs |   121.19 |    7.52 |         - |          NA |
