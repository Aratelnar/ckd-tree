// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Serialization;

namespace cKd_Tree
{
    /// <summary>
    /// Manages a compact array of bit values, which are represented as <see cref="bool"/>, where
    /// <see langword="true"/> indicates that the bit is on (1) and <see langword="false"/> indicates
    /// the bit is off (0).
    /// </summary>
    [Serializable]
    [TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public struct BitArray
    {
        public int Size => _array.Length;
        /// <summary>sizeof(int) * 8</summary>
        private const int BitsPerInt32 = 32;

        /// <summary>sizeof(byte) * 8</summary>
        private const int BitsPerByte = 8;

        /// <summary>The array of bytes used to store bits.</summary>
        /// <remarks>
        /// The array is allocated to hold enough bytes to store the specified number of bits, rounded up to the nearest multiple
        /// of sizeof(int). The last four bytes that might contain valid bits are always kept in a state where the unused bits
        /// are cleared, such that AsBytes and CopyTo operations will not show any set bits that are not actually set.
        /// </remarks>
        internal byte[] _array;

        /// <summary>The number of bits in the array.</summary>
        private int _bitLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitArray"/> class that can hold the specified
        /// number of bit values, which are initially set to false.
        /// </summary>
        /// <param name="length">The number of bit values in the new <see cref="BitArray"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
        public BitArray(int length)
            : this(length, false) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="BitArray"/> class that can hold the specified number of
        /// bit values, which are initially set to the specified value.
        /// </summary>
        /// <param name="length">The number of bit values in the new <see cref="BitArray"/>.</param>
        /// <param name="defaultValue">The Boolean value to assign to each bit.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
        public BitArray(int length, bool defaultValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            _array = AllocateByteArray(length);
            _bitLength = length;

            if (defaultValue)
            {
                Array.Fill(_array, (byte) 0xFF);
                ClearHighExtraBits();
            }
        }

        private void ClearHighExtraBits()
        {
            var (index, extraBits) = Math.DivRem((uint) _bitLength, BitsPerInt32);
            if (extraBits != 0)
            {
                MemoryMarshal.Cast<byte, int>((Span<byte>) _array)[(int) index] &=
                    ReverseIfBE((1 << (int) extraBits) - 1);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BitArray"/> class that contains bit values copied
        /// from the specified array of bytes.
        /// </summary>
        /// <param name="bytes">An array of bytes containing the values to copy, where each byte represents eight consecutive bits.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ArgumentException">The length of <paramref name="bytes"/> in bits is greater than <see cref="int.MaxValue"/>.</exception>
        public BitArray(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);

            _bitLength = bytes.Length * BitsPerByte;
            _array = AllocateByteArray(_bitLength);

            Array.Copy(bytes, _array, bytes.Length);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BitArray"/> class that contains bit values
        /// copied from the specified array of Booleans.
        /// </summary>
        /// <param name="values">An array of Booleans to copy.</param>
        /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
        public BitArray(bool[] values)
        {
            ArgumentNullException.ThrowIfNull(values);

            _array = AllocateByteArray(values.Length);
            _bitLength = values.Length;

            uint i = 0;

            if (!BitConverter.IsLittleEndian || values.Length < Vector256<byte>.Count)
            {
                goto Remainder;
            }

            // Comparing with 1s would get rid of the final negation, however this would not work for some CLR bools
            // (true for any non-zero values, false for 0) - any values between 2-255 will be interpreted as false.
            // Instead, We compare with zeroes (== false) then negate the result to ensure compatibility.

            ref var arrayRef = ref MemoryMarshal.GetArrayDataReference(_array);
            ref var value = ref Unsafe.As<bool, byte>(ref MemoryMarshal.GetArrayDataReference(values));
            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= (uint) values.Length - Vector512<byte>.Count; i += (uint) Vector512<byte>.Count)
                {
                    var vector = Vector512.LoadUnsafe(ref value, i);
                    var isFalse = Vector512.Equals(vector, Vector512<byte>.Zero);

                    var result = isFalse.ExtractMostSignificantBits();
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref arrayRef, sizeof(ulong) * (i / 64u)), ~result);
                }
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= (uint) values.Length - Vector256<byte>.Count; i += (uint) Vector256<byte>.Count)
                {
                    var vector = Vector256.LoadUnsafe(ref value, i);
                    var isFalse = Vector256.Equals(vector, Vector256<byte>.Zero);

                    var result = isFalse.ExtractMostSignificantBits();
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref arrayRef, sizeof(uint) * (i / 32u)), ~result);
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= (uint) values.Length - Vector128<byte>.Count * 2u; i += (uint) Vector128<byte>.Count * 2u)
                {
                    var lowerVector = Vector128.LoadUnsafe(ref value, i);
                    var lowerIsFalse = Vector128.Equals(lowerVector, Vector128<byte>.Zero);
                    var lowerResult = lowerIsFalse.ExtractMostSignificantBits();

                    var upperVector = Vector128.LoadUnsafe(ref value, i + (uint) Vector128<byte>.Count);
                    var upperIsFalse = Vector128.Equals(upperVector, Vector128<byte>.Zero);
                    var upperResult = upperIsFalse.ExtractMostSignificantBits();

                    Unsafe.WriteUnaligned(
                        ref Unsafe.Add(ref arrayRef, sizeof(uint) * (i / 32u)),
                        ~((upperResult << 16) | lowerResult));
                }
            }

            Remainder:
            for (; i < (uint) values.Length; i++)
            {
                if (values[i])
                {
                    var (byteIndex, bitOffset) = Math.DivRem(i, BitsPerByte);
                    _array[byteIndex] |= (byte) (1 << (int) bitOffset);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BitArray"/> class that contains bit values
        /// copied from the specified array of 32-bit integers.
        /// </summary>
        /// <param name="values">An array of integers containing the values to copy, where each integer represents 32 consecutive bits.</param>
        /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
        /// <exception cref="ArgumentException">The length of <paramref name="values"/> in bits is greater than <see cref="int.MaxValue"/>.</exception>
        /// <remarks>
        /// The number in the first <paramref name="values"/> array element represents bits 0 through 31, the second number in the array represents
        /// bits 32 through 63, and so on. The Least Significant Bit of each integer represents the lowest index value:
        /// "<paramref name="values"/>[0] &amp; 1" represents bit 0, "<paramref name="values"/>[0] &amp; 2" represents bit 1,
        /// "<paramref name="values"/>[0] &amp; 4" represents bit 2, and so on.
        /// </remarks>
        public BitArray(int[] values)
        {
            ArgumentNullException.ThrowIfNull(values);

            _bitLength = values.Length * BitsPerInt32;
            _array = AllocateByteArray(_bitLength);

            if (BitConverter.IsLittleEndian)
            {
                MemoryMarshal.AsBytes(values).CopyTo(_array);
            }
            else
            {
                BinaryPrimitives.ReverseEndianness(values, MemoryMarshal.Cast<byte, int>((Span<byte>) _array));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BitArray"/> class that contains bit values copied from the specified BitArray.
        /// </summary>
        /// <param name="bits">The <see cref="BitArray"/> to copy.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bits"/> is null.</exception>
        public BitArray(BitArray bits)
        {
            _bitLength = bits._bitLength;
            _array = AllocateByteArray(_bitLength);

            Array.Copy(bits._array, _array, _array.Length);
        }

        /// <summary>
        /// Gets or sets the value of the bit at a specific position in the <see cref="BitArray"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the value to get or set.</param>
        /// <returns>The value of the bit at position <paramref name="index"/>.</returns>
        /// <returns>The value of the bit at position <paramref name="index"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is greater than or equal to <see cref="Count"/>.</exception>
        public bool this[int index]
        {
            get => Get(index);
            set => Set(index, value);
        }

        /// <summary>
        /// Gets the value of the bit at a specific position in the <see cref="BitArray"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the value to get.</param>
        /// <returns>The value of the bit at position <paramref name="index"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is greater than or equal to <see cref="Count"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(int index)
        {
            var (byteIndex, bitOffset) = Math.DivRem((uint) index, BitsPerByte);
            return (_array[byteIndex] & (1 << (int) bitOffset)) != 0;
        }

        /// <summary>
        /// Sets the value of the bit at a specific position in the <see cref="BitArray"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the value to get.</param>
        /// <param name="value">The Boolean value to assign to the bit.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is greater than or equal to <see cref="Count"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, bool value)
        {
            var (byteIndex, bitOffset) = Math.DivRem((uint) index, BitsPerByte);

            ref var segment = ref _array[byteIndex];
            var bitMask = (byte) (1 << (int) bitOffset);
            if (value)
            {
                segment |= bitMask;
            }
            else
            {
                segment &= (byte) ~bitMask;
            }
        }

        /// <summary>
        /// Gets or sets the number of elements in the <see cref="BitArray"/>.
        /// </summary>
        /// <value>The number of elements in the <see cref="BitArray"/>.</value>
        /// <exception cref="ArgumentOutOfRangeException">The property is set to a value that is less than zero.</exception>
        public int Length => _bitLength;

        /// <summary>Determines the number of <see cref="int"/>s required to store <paramref name="bitLength"/> bits.</summary>
        private static int GetInt32ArrayLengthFromBitLength(int bitLength)
        {
            return (int) (((uint) bitLength + 31u) >> 5);
        }

        /// <summary>Determines the number of <see cref="byte"/>s required to store <paramref name="bitLength"/> bits.</summary>
        internal static int GetByteArrayLengthFromBitLength(int bitLength)
        {
            return (int) (((uint) bitLength + 7u) >> 3);
        }

        /// <summary>Rounds <paramref name="value"/> up to a multiple of sizeof(int).</summary>
        private static int RoundUpToMultipleSizeOfInt32(int value) =>
            (value + (sizeof(int) - 1)) & ~(sizeof(int) - 1);

        private static int GetAlignedByteArrayLength(int bitLength) =>
            // Always allocate in groups of sizeof(int) bytes so that we can use MemoryMarshal.Cast<byte, int>
            // to manipulate as ints when desired.
            RoundUpToMultipleSizeOfInt32(GetByteArrayLengthFromBitLength(bitLength));

        /// <summary>Allocates a new byte array of the specified bit length, rounded up to the nearest multiple of sizeof(int).</summary>
        private static byte[] AllocateByteArray(int bitLength)
        {
            var byteLength = GetAlignedByteArrayLength(bitLength);
            return bitLength != 0 ? new byte[byteLength] : [];
        }

        /// <summary>Nop on little endian, reverses the endianness of <paramref name="value"/> on big endian.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReverseIfBE(int value) =>
            BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);

        public int Rank(int index)
        {
            var result = 0;
            var intArray = MemoryMarshal.Cast<byte, uint>(_array);
            var (byteIndex, bitOffset) = Math.DivRem(index, BitsPerInt32);
            byteIndex = Math.Min(intArray.Length, byteIndex);
            for (var index1 = 0; index1 < byteIndex; index1++)
            {
                var i = intArray[index1];
                result += BitOperations.PopCount(i);
            }

            if (bitOffset != 0 && byteIndex < intArray.Length)
            {
                var mask = (uint)(1 << bitOffset) - 1;
                result += BitOperations.PopCount(intArray[byteIndex] & mask);
            }

            return result;
        }
    }
}