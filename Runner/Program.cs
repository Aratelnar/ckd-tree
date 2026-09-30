using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using cKd_Tree;
using TreeTests;

namespace Runner;

class Program
{
    private static (uint, uint)[] points;
    private static readonly Random random = new(42);

    private static int[] Sizes =
    [
        250_000,
        1_000_000,
        2_000_000,
        3_000_000,
        4_000_000,
        5_000_000,
        6_000_000,
        7_000_000,
        8_000_000,
        9_000_000,
        10_000_000
    ];

    private static int[] FieldSizes = [1 << 14, 1 << 15];

    static void Main(string[] args)
    {
        foreach (var fieldSize in FieldSizes)
        {
            foreach (var size in Sizes)
            {
                GC.Collect();
                points = GenerateUniformRandomPoints(random, fieldSize).Distinct().Take(size).ToArray();
                Console.WriteLine("---Uniform---");
                var points2 = new SortedSet<(int, int)>(points.Select(p => ((int) p.Item1, (int) p.Item2)));
                Console.WriteLine($"Field Size: {fieldSize}; Points Count: {size}");
                var baselineSize = 2 * points.Length * Math.Ceiling(Math.Log2(size));
                Console.WriteLine($"Density: {100d * size / fieldSize / fieldSize}; Baseline Size: {baselineSize}");

                var ikd = Implicit2DTree<uint>.Build(points).Size * 8;
                Console.WriteLine($"IKD Size: {ikd}; Compress: {100d - 100d * ikd / baselineSize}");
                var ckd = Compact2DTree<long>.Build(points.Select(p => ((long) p.Item1, (long) p.Item2))).Size * 8;
                Console.WriteLine($"CKD Size: {ckd}; Compress: {100d - 100d * ckd / baselineSize}");
                var k2t = K2TreeBuilder.Construct(points2, size, 2).Size * 8;
                Console.WriteLine($"K2T Size: {k2t}; Compress: {100d - 100d * k2t / baselineSize}");


                GC.Collect();
                points = GenerateGaussianRandomPoints(random, fieldSize).Distinct().Take(size).ToArray();
                Console.WriteLine("---Gaussian---");
                points2 = new SortedSet<(int, int)>(points.Select(p => ((int) p.Item1, (int) p.Item2)));
                Console.WriteLine($"Field Size: {fieldSize}; Points Count: {size}");
                baselineSize = 2 * points.Length * Math.Ceiling(Math.Log2(size));
                Console.WriteLine($"Density: {100d * size / fieldSize / fieldSize}; Baseline Size: {baselineSize}");

                ikd = Implicit2DTree<uint>.Build(points).Size * 8;
                Console.WriteLine($"IKD Size: {ikd}; Compress: {100d - 100d * ikd / baselineSize}");
                ckd = Compact2DTree<long>.Build(points.Select(p => ((long) p.Item1, (long) p.Item2))).Size * 8;
                Console.WriteLine($"CKD Size: {ckd}; Compress: {100d - 100d * ckd / baselineSize}");
                k2t = K2TreeBuilder.Construct(points2, size, 2).Size * 8;
                Console.WriteLine($"K2T Size: {k2t}; Compress: {100d - 100d * k2t / baselineSize}");
            }
        }

        // new Benchmarks();
        // var summary = BenchmarkRunner.Run<Benchmarks>();
    }

    private static IEnumerable<(uint, uint)> GenerateUniformRandomPoints(Random random, int size)
    {
        while (true)
        {
            var x = (uint) random.Next(size);
            var y = (uint) random.Next(size);

            yield return (x, y);
        }
    }

    private static IEnumerable<(uint, uint)> GenerateGaussianRandomPoints(Random random, int size)
    {
        while (true)
        {
            var x = random.NextDouble();
            var y = random.NextDouble();

            var dx = Math.Sqrt(-2 * Math.Log(x)) * Math.Sin(2 * Math.PI * y);
            var dy = Math.Sqrt(-2 * Math.Log(x)) * Math.Cos(2 * Math.PI * y);

            yield return ((uint) (dx * size / 2 + size / 2), (uint) (dy * size / 2 + size / 2));
        }
    }
}