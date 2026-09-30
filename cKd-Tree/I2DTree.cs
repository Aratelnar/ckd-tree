using System.Numerics;

namespace cKd_Tree;


/// <summary>
/// Интерфейс, описывающий структуру Kd-дерева в 2 мерном случае.
/// </summary>
/// <typeparam name="T">Тип, который должен следовать структуре</typeparam>
/// <typeparam name="TNum">Тип чисел для координат, нужен для общей обработки всех числовых типов</typeparam>
public interface I2DTree<T, TNum> : IEnumerable<(TNum, TNum)> where T : struct, I2DTree<T, TNum>
    where TNum : struct, INumber<TNum>
{
    public (TNum, TNum) Root { get; }
    public T? Left { get; }
    public T? Right { get; }

    /// <summary>
    /// Функция возвращающая последовательность всех точек в дереве слева направо.
    /// Необходима для тестов
    /// </summary>
    public IEnumerable<(TNum, TNum)> ToEnumerable()
    {
        foreach (var item in Left?.ToEnumerable() ?? ArraySegment<(TNum, TNum)>.Empty)
            yield return item;
        yield return Root;
        foreach (var item in Right?.ToEnumerable() ?? ArraySegment<(TNum, TNum)>.Empty)
            yield return item;
    }
}

/// <summary>
/// Класс расширений для Kd-дерева, позволяющий описать запросы без зависимости логики хранения данных
/// </summary>
public static class I2DTreeExtensions
{
    /// <summary>
    /// Запрос на проверку принадлежности точки дереву
    /// </summary>
    /// <param name="tree">Kd-дерево</param>
    /// <param name="point">Точка</param>
    /// <typeparam name="T">Тип дерева</typeparam>
    /// <typeparam name="TNum">Тип чисел</typeparam>
    public static bool ContainsPoint<T, TNum>(this T tree, (TNum, TNum) point)
        where TNum : struct, INumber<TNum>
        where T : struct, I2DTree<T, TNum>
    {
        return ContainsInternal(tree, point, 0);

        // Эта функция позволяет добавить аргумент, не давая над ним контроль пользователю
        bool ContainsInternal(T? tree, (TNum, TNum) point, int dimensionIndex)
        {
            while (true)
            {
                if (tree is null) return false;
                if (tree.Value.Root == point) return true;
                var cr = tree.Value.Root.At(dimensionIndex);
                var cp = point.At(dimensionIndex);
                if (cp > cr) // не ищем в левом поддереве, если точки там быть не может
                {
                    tree = tree.Value.Right;
                    dimensionIndex = (dimensionIndex + 1) % 2;
                    continue;
                }

                if (cp < cr) // не ищем в правом поддереве, если точки там быть не может
                {
                    tree = tree.Value.Left;
                    dimensionIndex = (dimensionIndex + 1) % 2;
                    continue;
                }

                // Точки на границе могут находиться в любом поддереве
                return ContainsInternal(tree.Value.Right, point, (dimensionIndex + 1) % 2) || ContainsInternal(tree.Value.Left, point, (dimensionIndex + 1) % 2);
            }
        }
    }

    /// <summary>
    /// Запрос на последовательность всех точек в заданной области
    /// </summary>
    /// <param name="tree">Kd-дерево</param>
    /// <param name="range">Область, прямоугольник, выровненный по осям координат </param>
    /// <typeparam name="T">Тип дерева</typeparam>
    /// <typeparam name="TNum">Тип чисел</typeparam>
    public static IEnumerable<(TNum, TNum)> Intersect<T, TNum>(this T tree, Range<TNum> range)
        where TNum : struct, INumber<TNum>
        where T : struct, I2DTree<T, TNum>
    {
        return IntersectInternal(tree, range, 0);

        // Эта функция позволяет добавить аргумент, не давая над ним контроль пользователю
        IEnumerable<(TNum, TNum)> IntersectInternal(T tree, Range<TNum> range, int dimensionIndex)
        {
            while (true)
            {
                var (x, y) = tree.Root;

                // отдаем точку в результат, если она лежит в области
                if (range.xMin <= x && x <= range.xMax && range.yMin <= y && y <= range.yMax) yield return tree.Root;
                var (left, right) = Split(range, tree.Root.At(dimensionIndex), dimensionIndex);
                if (left is not null && tree.Left.HasValue) // поиск в левом поддереве, если область с ним пересекается
                    foreach (var item in IntersectInternal(tree.Left.Value, left.Value, (dimensionIndex + 1) % 2))
                        yield return item;
                if (right is not null && tree.Right.HasValue) // поиск в правом поддереве, если область с ним пересекается
                {
                    tree = tree.Right.Value;
                    range = right.Value;
                    dimensionIndex = (dimensionIndex + 1) % 2;
                    continue;
                }

                break;
            }
        }

        // Эта функция делит область на две по заданной линии
        // line - координата линии
        // dimensionIndex - номер оси
        // возвращает опциональные разделения на случай непересечения линии и области
        (Range<TNum>?, Range<TNum>?) Split(Range<TNum> range, TNum line, int dimensionIndex)
        {
            if (dimensionIndex == 0)
            {
                if (range.xMin > line) return (null, range);
                if (range.xMax < line) return (range, null);
                return (range with {xMax = line - TNum.One}, range with {xMin = line});
            }

            if (dimensionIndex == 1)
            {
                if (range.yMin > line) return (null, range);
                if (range.yMax < line) return (range, null);
                return (range with {yMax = line - TNum.One}, range with {yMin = line});
            }

            return (null, null);
        }
    }
}

/// <summary>
/// Структура, описывающая прямоугольную область в 2-х мерном случае
/// </summary>
/// <typeparam name="TNum">Тип чисел</typeparam>
public readonly record struct Range<TNum>(TNum xMin, TNum yMin, TNum xMax, TNum yMax) where TNum : struct, INumber<TNum>;