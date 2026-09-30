using System.Numerics;
using cKd_Tree;

namespace TreeTests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void ShouldBuildImplicitTreeCorrect()
    {
        (BigInteger, BigInteger)[] points = [(8, 6), (4, 7), (10, 3), (2, 11), (5, 3), (12, 8), (2, 5)];
        (BigInteger, BigInteger)[] expected = [(2, 5), (4, 7), (2, 11), (5, 3), (10, 3), (8, 6), (12, 8)];
        var tree = Implicit2DTree<BigInteger>.Build(points);
        Assert.That(tree.Values.ToArray(), Is.EquivalentTo(expected));
    }

    [Test]
    public void ShouldBuildCompactTreeCorrect()
    {
        (BigInteger, BigInteger)[] points = [(8, 6), (4, 7), (10, 3), (2, 11), (5, 3), (12, 8), (2, 5)];
        (BigInteger, BigInteger)[] expected = [(2, 5), (4, 7), (2, 11), (5, 3), (10, 3), (8, 6), (12, 8)];
        var tree = Compact2DTree<BigInteger>.Build(points);
        Assert.That(tree.ToArray(), Is.EquivalentTo(expected));
    }

    [Test]
    public void ShouldQueryCompactTreeCorrect()
    {
        (BigInteger, BigInteger)[] points = [(8, 6), (4, 7), (10, 3), (2, 11), (5, 3), (12, 8), (2, 5)];
        var tree = Compact2DTree<BigInteger>.Build(points);
        Assert.That(tree.ContainsPoint((new BigInteger(4), 7)), Is.True);
        Assert.That(tree.ContainsPoint((new BigInteger(10), 3)), Is.True);
        Assert.That(tree.ContainsPoint((new BigInteger(4), 8)), Is.False);
        Assert.That(tree.ContainsPoint((new BigInteger(15), 7)), Is.False);
    }

    [Test]
    public void ShouldRangeQueryCompactTreeCorrect()
    {
        (BigInteger, BigInteger)[] points = [(8, 6), (4, 7), (10, 3), (2, 11), (5, 3), (12, 8), (2, 5)];
        (BigInteger, BigInteger)[] expected = [(8, 6), (10, 3), (5, 3)];
        var tree = Compact2DTree<BigInteger>.Build(points);
        var range = new Range<BigInteger>(3, 2, 11, 6);
        Assert.That(tree.Intersect(range), Is.EquivalentTo(expected));
    }


    [TestCaseSource(nameof(SpirarCodes))]
    public void ShouldBuildSpiralCodeCorrect((int, int) point, int expected)
    {
        Assert.That(point.SpiralCodeFrom((0, 0)), Is.EqualTo(expected));
    }
    
    [TestCaseSource(nameof(SpirarCodes))]
    public void ShouldBuildFromSpiralCodeCorrect((int, int) expected, int code)
    {
        Assert.That((0,0).AddSpiralCode(code), Is.EqualTo(expected));
    }
    
    [Test]
    public void ShouldBuildFromSpiralCodeCorrect2()
    {
        Assert.That((35,8).AddSpiralCode(15), Is.EqualTo((34, 6)));
    }
    
    [Test]
    public void ShouldBuildDAC()
    {
        long[] array = [700,25,644,50001];
        var dac = DirectAccessCodes<long>.Build(array);
        Assert.That(dac[3], Is.EqualTo(array[3]));
        Assert.That(dac[1], Is.EqualTo(array[1]));
        Assert.That(dac[0], Is.EqualTo(array[0]));
        Assert.That(dac[2], Is.EqualTo(array[2]));
    }

    [Test]
    public void ShouldBuildK2TreeCorrect()
    {
        var rand = new Random();
        (int, int)[] points = [(8, 6), (4, 7), (10, 3), (2, 11), (5, 3), (12, 8), (2, 5)];
        var size = 16;
        var matrix = new bool[size, size];
        foreach (var point in points) matrix[point.Item1, point.Item2] = true;
        var tree = K2TreeBuilder.Construct(matrix, 2);

        foreach (var point in points)
        {
            Assert.That(tree[(uint)point.Item1, (uint)point.Item2], Is.True);
        }

        for (var i = 0; i < 1000; i++)
        {
            var x = rand.Next(16);
            var y = rand.Next(16);
            Assert.That(tree[(uint)x, (uint)y], Is.EqualTo(points.Contains((x,y))));
        }
    }

    [Test]
    public void ShouldBuildK2TreeCorrect_FromList()
    {
        var rand = new Random();
        (int, int)[] points = [(8, 6), (4, 7), (10, 3), (2, 11), (5, 3), (12, 8), (2, 5)];
        var tree = K2TreeBuilder.Construct(new SortedSet<(int, int)>(points), 16, 2);

        foreach (var point in points)
        {
            Assert.That(tree[(uint)point.Item1, (uint)point.Item2], Is.True);
        }

        for (var i = 0; i < 1000; i++)
        {
            var x = rand.Next(16);
            var y = rand.Next(16);
            Assert.That(tree[(uint)x, (uint)y], Is.EqualTo(points.Contains((x,y))));
        }
    }



    private static object[] SpirarCodes =
    [
        new object[] { (0, 0), 0 },
        new object[] { (1, 0), 1 },
        new object[] { (1, -1), 2 },
        new object[] { (0, -1), 3 },
        new object[] { (-1, -1), 4 },
        new object[] { (-1, 0), 5 },
        new object[] { (-1, 1), 6 },
        new object[] { (0, 1), 7 },
        new object[] { (1, 1), 8 },
        new object[] { (2, 1), 9 },
        new object[] { (2, 0), 10 },
        new object[] { (2, -1), 11 },
        new object[] { (2, -2), 12 },
        new object[] { (1, -2), 13 },
        new object[] { (0, -2), 14 },
        new object[] { (-1, -2), 15 },
        new object[] { (-2, -2), 16 },
        new object[] { (-2, -1), 17 },
        new object[] { (-2, 0), 18 },
        new object[] { (-2, 1), 19 },
        new object[] { (-2, 2), 20 },
        new object[] { (-1, 2), 21 },
        new object[] { (0, 2), 22 },
        new object[] { (1, 2), 23 },
        new object[] { (2, 2), 24 },
    ];
}