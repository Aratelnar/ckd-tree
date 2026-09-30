using System.Collections;
using System.Numerics;

namespace cKd_Tree;

/// <summary>
/// Интерфейс, описывающий требования для структуры Directly Accesseble Codes.
/// В сущности, структура должна уметь отдавать элементы по индексу
/// </summary>
/// <typeparam name="T">Тип чисел, которые хранит структура</typeparam>
public interface IDirectAccessCodes<TSelf, out T> : IReadOnlyCollection<T> where T : struct, IBinaryInteger<T>
    where TSelf : IDirectAccessCodes<TSelf, T>
{
    T this[int index] { get; }
    TSelf this[Range range] { get; }
}

/// <summary>
/// Структура Directly Accesseble Codes.
/// Позволяет считывать положительные целые числа за O(log m), где m - максимум из чисел, и хранить их без большинства ведущих нолей
/// </summary>
/// <typeparam name="T">Тип чисел, которые хранит структура</typeparam>
public record struct DirectAccessCodes<T> : IDirectAccessCodes<DACView<T>, T> where T : struct, IBinaryInteger<T>
{
    private static int BlockSize = 7;
    private static int blockBit = 1 << BlockSize;
    private static T blockBitT = T.CreateChecked(blockBit);
    private static int blockMask = 0xFF;
    private static T blockMaskT = T.CreateChecked(blockMask);

    public unsafe int Size => sizeof(DirectAccessCodes<T>) + CStreams.Sum(arr => arr.Length) + BMaps.Sum(b => b.Size);

    /// <summary>
    /// Массив C-Streams, поскольку размер блока, на которые разбиваются числа, равен 7,
    /// само значение с флагом продолжения помещается в byte, из-за чего каждый C-Stream еквивалентен byte[]
    /// </summary>
    byte[][] CStreams;

    /// <summary>
    /// Массив битовых масок для каждого C-Stream
    /// </summary>
    BitArray[] BMaps;

    public T this[int index] => Get(index);

    public DACView<T> this[Range range] => new(this, range.Start.GetOffset(Count), range.End.GetOffset(Count));

    /// <summary>
    /// Функция реконструкции значения из хранимых данных
    /// </summary>
    /// <returns></returns>
    private T Get(int index)
    {
        var value = T.Zero;
        var shift = 0;

        // Собираем биты значения пока флаг не нулевой
        var b = true;
        for (var i = 0; b; i++)
        {
            b = BMaps[i].Get(index);
            value += T.CreateChecked((CStreams[i][index] & blockMask) << shift); // припысывание текущих битов слева
            shift += BlockSize;

            // Поскольку в каждом следующем потоке и маске хранятся лишь данные тех чисел, что не закончились ранее
            // необходимо менять индекс поиска на количество незаконченных чисел ранее в текущем массиве
            if (b) index = BMaps[i].Rank(index); // быстрый подсчёт количество единиц в массиве битов
        }

        return value;
    }

    /// <summary>
    /// Статический фабричный метод создания структуры из коллекции чисел
    /// </summary>
    /// <returns></returns>
    public static DirectAccessCodes<T> Build(IReadOnlyCollection<T> values)
    {
        // Сначала мы разбиваем каждое число на последовательность блоков
        // поскольку потоки хранят данные по уровням, а не по числам
        // необходимо изменить порядок данных в последовательностях
        var cs = Transpose(values.Select(GetCSequence)).Select(e => e.ToArray()).ToArray();
        var cs2 = cs.Select(c => c.Select(c1 => c1.Item1).ToArray()).ToArray();
        // по готовым потокам получаем битовые маски
        var bm = cs.Select(CreateBitArrayFromCStream).ToArray();
        return new DirectAccessCodes<T> {CStreams = cs2, BMaps = bm};
    }

    /// <summary>
    /// Сбор битовой маски из С-потока
    /// </summary>
    /// <param name="cStream"></param>
    /// <returns></returns>
    private static BitArray CreateBitArrayFromCStream((byte, bool)[] cStream)
    {
        return new BitArray(cStream.Select(c => c.Item2).ToArray());
    }

    /// <summary>
    /// Разбиение числа на последовательность блоков по <see cref="BlockSize"/> битов
    /// Каждый блок начинается с флага завершенности, 1 - если есть блоки далее, 0 - если блок последний
    /// </summary>
    /// <returns></returns>
    private static IEnumerable<(byte, bool)> GetCSequence(T value)
    {
        while (value >= blockBitT)
        {
            yield return (byte.CreateChecked(value & blockMaskT), true); // маска на младше 7 битов числа и выставление флага в единицу
            value >>= BlockSize; // сдвиг числа, для обработки следующего блока
        }

        yield return (byte.CreateChecked(value), false); // последний блок идет с флагом 0
    }

    /// <summary>
    /// Меняет порядок хранения данных в списке списков. Пример:
    /// <example>
    ///     [
    ///         [1,2,3,4],
    ///         [5,6],
    ///         [7,8,9]
    ///     ]
    /// переходит в<br/>
    ///     [
    ///         [1,5,7],
    ///         [2,6,8],
    ///         [3,9],
    ///         [4]
    ///     ]
    /// </example>
    /// </summary>
    /// <returns></returns>
    private static IEnumerable<IEnumerable<(byte, bool)>> Transpose(IEnumerable<IEnumerable<(byte, bool)>> value)
    {
        var enumerators = value.Select(i => i.GetEnumerator()).ToArray();
        enumerators = enumerators.Where(e => e.MoveNext()).ToArray();
        while (enumerators.Length > 0)
        {
            yield return enumerators.Select(e => e.Current);
            enumerators = enumerators.Where(e => e.MoveNext()).ToArray();
        }
    }

    #region ICollection

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < CStreams[0].Length; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => CStreams[0].Length;

    #endregion
}

/// <summary>
/// Специальная структура-обертка, для доступа к под массиву данных структуры.
/// Позволяет получить структуру меньшей длины, не копируя изначальную структуру
/// </summary>
/// <typeparam name="T">Тип чисел, которые хранит структура</typeparam>
public record struct DACView<T> : IDirectAccessCodes<DACView<T>, T> where T : struct, IBinaryInteger<T>
{
    private DirectAccessCodes<T> _array;
    private int _start;
    private int _length;

    public unsafe int Size => sizeof(DACView<T>) + _array.Size;

    public DACView(DirectAccessCodes<T> array, int start, int end)
    {
        _array = array;
        _start = start;
        _length = end - start;
    }

    public IEnumerator<T> GetEnumerator() => _array.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => _length;

    public T this[int index] => _array[index + _start];

    public DACView<T> this[Range range] => this with
    {
        _start = _start + range.Start.GetOffset(_length),
        _length = range.GetOffsetAndLength(_length).Length
    };
}