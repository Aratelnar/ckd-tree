using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using cKd_Tree;

namespace TreeTests;

[MemoryDiagnoser]
public class Benchmarks
{
    private static readonly Random random = new(42);
    private static (uint, uint)[] points;
    private const int size = 1 << 15;

    private Implicit2DTree<uint> Implicit2DTree;
    private Compact2DTree<long> Compact2DTree;
    private K2Tree K2TreeImpl;

    private (uint, uint)[] point;

    public Benchmarks()
    {
        points = GenerateRandomPoints(random, size).Distinct().Take(10_000_000).ToArray();
        var points2 = new SortedSet<(int, int)>(points.Select(p => ((int) p.Item1, (int) p.Item2)));
        Console.WriteLine("Get points");

        Implicit2DTree = Implicit2DTree<uint>.Build(points);
        Console.WriteLine(Implicit2DTree.Size);
        Console.WriteLine(Compress(Implicit2DTree.Size * 8));
        Compact2DTree = Compact2DTree<long>.Build(points.Select(p => ((long) p.Item1, (long) p.Item2)));
        Console.WriteLine(Compact2DTree.Size);
        Console.WriteLine(Compress(Compact2DTree.Size * 8));
        K2TreeImpl = K2TreeBuilder.Construct(points2, size, 2);
        Console.WriteLine(K2TreeImpl.Size);
        Console.WriteLine(Compress(K2TreeImpl.Size * 8));


        double Compress(double value1) => 100 - 100 * value1 / (2 * points.Length * Math.Ceiling(Math.Log2(size)));

        point = GenerateRandomPoints(random, size).Take(1_000).ToArray();
    }

    [Benchmark(Baseline = true)]
    public void IkdTree()
    {
        foreach (var i in point)
            Implicit2DTree.ContainsPoint(i);
    }

    [Benchmark]
    public void CkdTree()
    {
        foreach (var i in point)
            Compact2DTree.ContainsPoint(((long) i.Item1, i.Item2));
    }

    [Benchmark]
    public void K2Tree()
    {
        foreach (var tuple in point)
            K2TreeImpl.ContainsPoint(tuple);
    }

    private IEnumerable<(uint, uint)> GenerateRandomPoints(Random random, int size)
    {
        while (true)
        {
            // var x = (uint) random.Next(size);
            // var y = (uint) random.Next(size);
            var x = random.NextDouble();
            var y = random.NextDouble();

            var dx = Math.Sqrt(-2 * Math.Log(x)) * Math.Sin(2 * Math.PI * y);
            var dy = Math.Sqrt(-2 * Math.Log(x)) * Math.Cos(2 * Math.PI * y);

            yield return ((uint) (dx * size / 2 + size / 2), (uint) (dy * size / 2 + size / 2));
            // yield return (x, y);
        }
    }
}