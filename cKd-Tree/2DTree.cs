using System.Collections;
using System.Numerics;

namespace cKd_Tree;

/// <summary>
/// Обобщенная структура Kd-дерева. Позволяет наиболее простым способом построить дерево в памяти, неэффективно, так как использует ссылки на объекты в куче.
/// Представляет собой узел дерева со значением в этом узле, и ссылками на правое и левой поддеревья.
/// </summary>
/// <param name="Root">Значение точки в узле</param>
/// <param name="Left">Левое поддерево (опционально)</param>
/// <param name="Right">Правое поддерево (опционально)</param>
/// <typeparam name="T">Тип чисел координат</typeparam>
public record General2DTree<T>((T, T) Root, General2DTree<T>? Left, General2DTree<T>? Right) : IEnumerable<(T, T)>
    where T : struct, INumber<T>
{
    public IEnumerator<(T, T)> GetEnumerator()
    {
        foreach (var item in Left ?? Enumerable.Empty<(T, T)>())
            yield return item;
        yield return Root;
        foreach (var item in Right ?? Enumerable.Empty<(T, T)>())
            yield return item;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// iKd-дерево (Implicit Kd-tree). Поскольку в различных статьях содержание дерева различается, эта версия хранит точки в одном упорядоченном массиве в изначальном виде.
/// Implicit в данном дереве означает, что структура дерева не указана явно в памяти:
/// Значение в корне поддерева - это медиана массива
/// Левое и правое поддеревья это подмассивы до и после медианы соответственно.
/// </summary>
/// <typeparam name="T">Тип чисел координат</typeparam>
public readonly record struct Implicit2DTree<T> : I2DTree<Implicit2DTree<T>, T> where T : struct, INumber<T>
{
    private const int DimensionsCount = 2;

    /// <summary>
    /// Memory используется как обертка над массивом, для избежания выделения памяти для поддеревьев в куче
    /// </summary>
    private readonly Memory<(T, T)> _values;

    public unsafe int Size => sizeof(Implicit2DTree<T>) + _values.Length * sizeof((T, T));

    public ReadOnlySpan<(T, T)> Values => _values.Span;

    private Implicit2DTree(Memory<(T, T)> values)
    {
        _values = values;
    }

    /// <summary>
    /// Статический фабричный метод, позволяющий построить iKd-дерево за O(d*n*log(n)) времени
    /// </summary>
    /// <returns></returns>
    public static Implicit2DTree<T> Build(IEnumerable<(T, T)> points)
    {
        // копирование данных и преобразование в удобный тип
        var pointsArrayForX = points.ToArray().AsSpan();
        var pointsArrayForY = pointsArrayForX.ToArray().AsSpan();

        // выделение доп памяти заранее для сортировки по методу sweep & partition
        var temp = new (T, T)[pointsArrayForX.Length];

        // сортировка по каждой координате
        pointsArrayForX.Sort((x, y) => x.CompareTo(y)); // Сортируем в лексикографическом порядке
        pointsArrayForY.Sort((x, y) => (x.Item2, x.Item1).CompareTo((y.Item2, y.Item1)));

        // построение сбалансированного Kd-дерева
        var generalTree = ConstructNode(pointsArrayForX, pointsArrayForY, 0, temp)!;

        // очистка явной структуры с помощью перечисления дерева в массив
        return new Implicit2DTree<T>(generalTree.ToArray());
    }

    /// <summary>
    /// Построение узла Kd-дерева по сортированным массивам точек
    /// </summary>
    /// <param name="pointRangeForX">Массив точек, отсортированных по X</param>
    /// <param name="pointRangeForY">Массив точек, отсортированных по Y</param>
    /// <param name="dimensionIndex">Индекс координаты по которой происходит разделение дерева</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static General2DTree<T>? ConstructNode(
        Span<(T, T)> pointRangeForX,
        Span<(T, T)> pointRangeForY,
        int dimensionIndex,
        Span<(T, T)> temp)
    {
        if (pointRangeForX.Length == 0) return null;
        // находим медиану массива
        var median = pointRangeForX.Length / 2;
        (T, T) root;
        switch (dimensionIndex)
        {
            case 0: // случай деления по координате X
                root = pointRangeForX[median];

                // если мы разделили массив по координате X
                // мы хотим, чтобы массив, отсортированный по Y, уважал это разбиение
                pointRangeForY.PartitionBy(root, dimensionIndex, temp);
                break;
            case 1: // случай деления по координате Y
                root = pointRangeForY[median];

                // аналогично в случае разбиения по Y
                pointRangeForX.PartitionBy(root, dimensionIndex, temp);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dimensionIndex));
        }

        // после того как мы разбили оба отсортированных массива по выбранному корню
        // мы можем рекурсивно запуститься от левой и правой половин массива
        return new General2DTree<T>(root,
            ConstructNode(
                pointRangeForX[..median],
                pointRangeForY[..median],
                (dimensionIndex + 1) % DimensionsCount,
                temp[..median]),
            ConstructNode(
                pointRangeForX[(median + 1)..],
                pointRangeForY[(median + 1)..],
                (dimensionIndex + 1) % DimensionsCount,
                temp[(median + 1)..])
        );
    }

    public (T, T) Root => _values.Span[_values.Length / 2];

    /// <summary>
    /// Левое поддерево, возвращает структуру над тем же массивом, новая память не выделяется
    /// </summary>
    public Implicit2DTree<T>? Left =>
        _values.Length / 2 > 0 ? new Implicit2DTree<T>(_values[..(_values.Length / 2)]) : null;

    /// <summary>
    /// Правое поддерево, возвращает структуру над тем же массивом, новая память не выделяется
    /// </summary>
    public Implicit2DTree<T>? Right =>
        _values.Length / 2 + 1 < _values.Length ? new Implicit2DTree<T>(_values[(_values.Length / 2 + 1)..]) : null;

    public IEnumerator<(T, T)> GetEnumerator() => ((I2DTree<Implicit2DTree<T>, T>) this).ToEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// cKd-дерево (Compact Kd-tree). Похоже на iKd-дерево, только хранит точки дерева в компактном виде.
/// Для компактности используются две идеи:
/// * Спиральные коды, для кодирования относительного смещения дочерних точек
/// * Directly Accessible Codes, для сжатого хранения массива целых неотрицательных чисел
/// </summary>
/// <typeparam name="T">Тип чисел координат</typeparam>
public readonly record struct Compact2DTree<T> : I2DTree<Compact2DTree<T>, T> where T : struct, IBinaryInteger<T>
{
    private readonly DACView<T> _values;
    public (T, T) Root { get; }

    public unsafe int Size => sizeof(Compact2DTree<T>) + _values.Size;

    private Compact2DTree(DACView<T> values, (T, T) root)
    {
        _values = values;
        Root = root;
    }

    /// <summary>
    /// Статический фабричный метод, позволяющий построить сKd-дерево за O(d*n*log(n)) времени
    /// </summary>
    /// <returns></returns>
    public static Compact2DTree<T> Build(IEnumerable<(T, T)> points)
    {
        var icd = Implicit2DTree<T>.Build(points); // строим iKd-дерево
        var codes = icd.ConstructSpiralCodes(icd.Root).ToArray(); // преобразуем точки в спиральные коды
        var dac = DirectAccessCodes<T>.Build(codes); // строим DAC по кодам
        return new Compact2DTree<T>(dac[..], icd.Root); // сохраняем DAC и корень дерева в структуру
    }

    /// <summary>
    /// Получение корня поддерева по текущему корню
    /// </summary>
    /// <returns></returns>
    private (T, T) RetrieveRoot((T, T) currentRoot, DACView<T> values)
    {
        var code = values[values.Count / 2]; // Получаем спиральный код корня поддерева values
        return currentRoot.AddSpiralCode(code); // Получения точки по текущему корню и спиральному коду
    }

    /// <summary>
    /// Левое поддерево, возвращает структуру над тем же массивом, новая память не выделяется
    /// </summary>
    public Compact2DTree<T>? Left =>
        _values.Count / 2 > 0
            ? new(_values[..(_values.Count / 2)], RetrieveRoot(Root, _values[..(_values.Count / 2)]))
            : null;

    /// <summary>
    /// Правое поддерево, возвращает структуру над тем же массивом, новая память не выделяется
    /// </summary>
    public Compact2DTree<T>? Right =>
        _values.Count / 2 + 1 < _values.Count
            ? new(_values[(_values.Count / 2 + 1)..], RetrieveRoot(Root, _values[(_values.Count / 2 + 1)..]))
            : null;

    public override string ToString() => "";

    public IEnumerator<(T, T)> GetEnumerator() => ((I2DTree<Compact2DTree<T>, T>) this).ToEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class Extensions
{
    /// <summary>
    /// Получение значения координаты точки по индексу координаты
    /// </summary>
    /// <typeparam name="T">Тип чисел</typeparam>
    public static T At<T>(this (T, T) tuple, int index) where T : INumber<T> => index switch
    {
        0 => tuple.Item1,
        1 => tuple.Item2
    };

    /// <summary>
    /// Разбиение массива по точке.
    /// Стабильно сортирует массив по признаку сравнения точек с координатой заданной точки в порядке:
    /// * точки с координатой меньше
    /// * точки с той же координатой
    /// * точки с координатой больше
    /// </summary>
    /// <param name="span">Массив, который нужно отсортировать</param>
    /// <param name="value">Точка, по которой сортируем</param>
    /// <param name="dimensionIndex">Индекс координаты</param>
    /// <param name="temp">Временный массив для записи</param>
    /// <typeparam name="T">Тип чисел координат</typeparam>
    public static void PartitionBy<T>(this Span<(T, T)> span, (T, T) value, int dimensionIndex,
        Span<(T, T)> temp) where T : INumber<T>
    {
        var comp = value.At(dimensionIndex);
        var ilt = 0;
        var igt = span.Length / 2 + 1;
        foreach (var val in span)
        {
            // записываем в три под области
            // если точка равна данной, записываем в середину temp
            // если точка в массиве по другой координате раньше данной, записываем в левую половину temp
            // если точка в массиве по другой координате позже данной, записываем в правую половину temp
            var compVal = val.At(dimensionIndex);
            if (compVal == comp)
            {
                var nextDimensionIndex = (dimensionIndex + 1) % 2;
                var comp2 = value.At(nextDimensionIndex);
                var compVal2 = val.At(nextDimensionIndex);

                if (compVal2 == comp2) temp[span.Length / 2] = val;
                else if (compVal2 < comp2) temp[ilt++] = val;
                else if (compVal2 > comp2) temp[igt++] = val;
            }
            else if (compVal < comp) temp[ilt++] = val;
            else if (compVal > comp) temp[igt++] = val;
        }

        temp.CopyTo(span); // копируем данные во входной массив
    }

    /// <summary>
    /// Вычисление спирального кода по двум точкам. Работает за O(1)
    /// </summary>
    /// <param name="point">Точка, до которой считаем спиральный код</param>
    /// <param name="root">Точка начала отсчета</param>
    /// <typeparam name="T">Тип чисел координат</typeparam>
    // ReSharper disable once ConvertToExtensionBlock
    public static T SpiralCodeFrom<T>(this (T X, T Y) point, (T X, T Y) root) where T : INumber<T>
    {
        var (dx, dy) = (point.X - root.X, point.Y - root.Y);
        var (mdx, mdy) = (T.Abs(dx), T.Abs(dy));
        var radius = T.Max(mdx, mdy); // Находим радиус квадрата, с центром в root и касающегося point
        var code = (radius - T.One) * radius *
                   T.CreateChecked(
                       4); // Считаем спиральный код, чтобы покрыть этот квадрат: (radius - 1) * radius * 8 / 2

        // В зависимости от стороны квадрата, которой касается point, высчитываем остаток кода
        if (dx > T.Zero && dy < mdx && dy >= -mdx)
            return code + radius - dy;
        if (dy < T.Zero && dx < mdy && dx >= -mdy)
            return code + T.CreateChecked(2) * radius + radius - dx;
        if (dx < T.Zero && dy <= mdx && dy > -mdx)
            return code + T.CreateChecked(4) * radius + radius + dy;
        if (dy > T.Zero && dx <= mdy && dx > -mdy)
            return code + T.CreateChecked(6) * radius + radius + dx;
        return code;
    }

    /// <summary>
    /// Получения точки по точке отсчета координат и спиральному коду. Работает за O(m), где m - длина спирального кода
    /// </summary>
    /// <param name="point">Точка отсчета координат</param>
    /// <param name="code">Спиральный код</param>
    /// <typeparam name="T">Тип чисел координат</typeparam>
    public static (T, T) AddSpiralCode<T>(this (T X, T Y) point, T code) where T : INumber<T>
    {
        var (x, y) = point;
        var maxRadius = code + T.One;
        var minRadius = T.Zero;

        // Бинарный поиск радиуса квадрата, касающегося искомой точки
        while (minRadius < maxRadius)
        {
            var mid = (maxRadius + minRadius) / T.CreateChecked(2);
            if (mid * (mid + T.One) * T.CreateChecked(4) >= code)
                maxRadius = mid;
            else
                minRadius = mid + T.One;
        }

        var radius = minRadius;
        if (radius == T.Zero) return point;

        // Вычитание кода, необходимого, чтобы покрыть квадрат
        var rem = code - radius * (radius - T.One) * T.CreateChecked(4);

        // Выделение координат в зависимости от стороны квадрата, которой касается искомая точка
        switch (int.CreateChecked(rem / (T.CreateChecked(2) * radius)))
        {
            case 0:
                return (x + radius, y + radius - rem);
            case 1:
                rem -= T.CreateChecked(2) * radius;
                return (x + radius - rem, y - radius);
            case 2:
                rem -= T.CreateChecked(4) * radius;
                return (x - radius, y + rem - radius);
            case 3:
                rem -= T.CreateChecked(6) * radius;
                return (x + rem - radius, y + radius);
            case 4:
                return (x + radius, y + radius);
            default:
                throw new ArgumentException();
        }
    }

    /// <summary>
    /// Расчет спиральных кодов в соответствии со структурой дерева.
    /// Спиральный код корня поддерева считается от корня его родительского дерева
    /// </summary>
    /// <param name="tree">Поддерево</param>
    /// <param name="root">Корень родительского дерева</param>
    /// <typeparam name="T">Тип дерева</typeparam>
    /// <typeparam name="TNum">Тип чисел координат</typeparam>
    /// <returns>Последовательность спиральных кодов для неявной структуры дерева</returns>
    public static IEnumerable<TNum> ConstructSpiralCodes<T, TNum>(this T tree, (TNum, TNum) root)
        where T : struct, I2DTree<T, TNum> where TNum : struct, INumber<TNum>
    {
        if (tree.Left != null)
            foreach (var code in ConstructSpiralCodes(tree.Left.Value, tree.Root))
                yield return code;
        yield return tree.Root.SpiralCodeFrom(root);
        if (tree.Right != null)
            foreach (var code in ConstructSpiralCodes(tree.Right.Value, tree.Root))
                yield return code;
    }
}