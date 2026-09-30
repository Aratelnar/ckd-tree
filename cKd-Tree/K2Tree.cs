using System.Collections;
using System.Formats.Asn1;
using System.Numerics;

namespace cKd_Tree;

public static class K2TreeBuilder
{
    public static K2Tree Construct(bool[,] matrix, int k)
    {
        var T = new List<bool>();
        var L = new List<bool>();
        var n = (int) Math.Pow(2, Math.Ceiling(Math.Log2(matrix.GetLength(0))));

        var quadQueue = new Queue<(int row, int col, int size)>();
        quadQueue.Enqueue((0, 0, n));
        while (quadQueue.Count > 0)
        {
            var (rRow, rCol, size) = quadQueue.Dequeue();
            size /= k;

            for (var i = 0; i < k; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    if (size == 1)
                    {
                        L.Add(matrix[rRow + i, rCol + j]);
                    }
                    else if (CheckNonEmpty(matrix, rRow + i * size, rCol + j * size, size))
                    {
                        T.Add(true);
                        quadQueue.Enqueue((rRow + i * size, rCol + j * size, size));
                    }
                    else
                    {
                        T.Add(false);
                    }
                }
            }
        }

        return new K2Tree(n, k, new BitArray(T.ToArray()), new BitArray(L.ToArray()));
    }

    public static K2Tree Construct(SortedSet<(int, int)> points, int n, int k)
    {
        var T = new List<bool>();
        var L = new List<bool>();

        var quadQueue = new Queue<(int row, int col, int size)>();
        quadQueue.Enqueue((0, 0, n));
        while (quadQueue.Count > 0)
        {
            var (rRow, rCol, size) = quadQueue.Dequeue();
            size /= k;

            for (var i = 0; i < k; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    if (size == 1)
                    {
                        L.Add(points.Contains((rRow + i, rCol + j)));
                    }
                    else if (CheckNonEmpty(points, rRow + i * size, rCol + j * size, size))
                    {
                        T.Add(true);
                        quadQueue.Enqueue((rRow + i * size, rCol + j * size, size));
                    }
                    else
                    {
                        T.Add(false);
                    }
                }
            }
        }

        return new K2Tree(n, k, new BitArray(T.ToArray()), new BitArray(L.ToArray()));
    }


    private static bool CheckNonEmpty(SortedSet<(int, int)> matrix, int r, int c, int s)
    {
        // проверяет, есть ли в подматрице s×s с левым верхним углом (r,c) хотя бы одна 1
        for (var i = 0; i < s; i++)
        {
            if (matrix.GetViewBetween((r + i, c), (r + i, c + s)).Count != 0)
                return true;
        }

        return false;
    }


    private static bool CheckNonEmpty(bool[,] matrix, int r, int c, int s)
    {
        // проверяет, есть ли в подматрице s×s с левым верхним углом (r,c) хотя бы одна 1
        for (var i = 0; i < s; i++)
        for (var j = 0; j < s; j++)
            if (matrix[r + i, c + j]) return true;

        return false;
    }
}

public struct K2Tree
{
    private uint N;
    private uint K = 2;
    private BitArray T;
    private BitArray L;

    public unsafe int Size => sizeof(K2Tree) + T.Size + L.Size;

    public K2Tree(int n, int k, BitArray t, BitArray l)
    {
        N = (uint)n;
        K = (uint)k;
        T = t;
        L = l;
    }

    public bool this[uint row, uint col] => GetValueAt(row, col);

    private bool GetValueAt(uint row, uint col)
    {
        if (T.Length == 0) return false;

        var currNode = -1;
        var currSize = N / K;

        while (currNode < T.Length)
        {
            var currRow = row / currSize;
            var currCol = col / currSize;
            var currChild = currRow * K + currCol;
            var currChildIndex = GetChildIndex(currNode, currChild);
            if (currChildIndex >= T.Length)
            {
                currNode = currChildIndex;
                break;
            }

            if (!T[currChildIndex]) return false;
            row %= currSize;
            col %= currSize;
            currNode = currChildIndex;
            currSize /= K;
        }

        currNode -= T.Length;
        return L[currNode];
    }

    public bool ContainsPoint((uint row, uint col) point) => GetValueAt(point.row, point.col);

    private int GetChildIndex(int node, uint i) => (int) (T.Rank(node + 1) * K * K + i);
}