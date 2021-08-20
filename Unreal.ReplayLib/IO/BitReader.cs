using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Unreal.ReplayLib.Memory;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.IO
{
    public unsafe class BitReader : FBitArchive
    {
        public override int Position
        {
            get => _position;
            protected set => _position = value;
        }

        private int _position;

        public int LastBit { get; private set; }


        private readonly int[] _tempLastBit = GetPool();
        private readonly int[] _tempPosition = GetPool();

        private static readonly ConcurrentQueue<int[]> PositionQueues = new();

        public int MarkPosition { get; private set; }

        public BitReader()
        {
        }

        public BitReader(byte* ptr, int byteCount, int bitCount) => CreateBitArray(ptr, byteCount, bitCount);

        public BitReader(bool* boolPtr, int bitCount)
        {
            Bits = boolPtr;
            LastBit = bitCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool AtEnd() => _position >= LastBit;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool CanRead(int count) => _position + count <= LastBit;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool PeekBit() => Bits[_position];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool ReadBit()
        {
            if (_position >= LastBit)
            {
                IsError = true;
                return false;
            }

            return Bits[_position++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadBitsToInt(int bitCount)
        {
            if (!CanRead(bitCount))
            {
                IsError = true;

                return 0;
            }

            var result = 0;
            for (var i = 0; i < bitCount; i++)
            {
                result |= (byte)(GetAsByte(_position + i) << i);
            }

            _position += bitCount;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ReadOnlyMemory<bool> ReadBits(int bitCount)
        {
            if (!CanRead(bitCount) || bitCount < 0)
            {
                IsError = true;
                return ReadOnlyMemory<bool>.Empty;
            }

            var result = _items.Slice(_position, bitCount);

            _position += bitCount;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ReadOnlyMemory<bool> ReadBits(uint bitCount) => ReadBits((int)bitCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool ReadBoolean() => ReadBit();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override byte PeekByte()
        {
            var result = ReadByte();
            _position -= 8;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ReadByteNoCheck()
        {
            var result = new byte();

            var pos = _position;
            result |= (GetAsByte(pos + 0));
            result |= (byte)(GetAsByte(pos + 1) << 1);
            result |= (byte)(GetAsByte(pos + 2) << 2);
            result |= (byte)(GetAsByte(pos + 3) << 3);
            result |= (byte)(GetAsByte(pos + 4) << 4);
            result |= (byte)(GetAsByte(pos + 5) << 5);
            result |= (byte)(GetAsByte(pos + 6) << 6);
            result |= (byte)(GetAsByte(pos + 7) << 7);
            _position += 8;

            return result;
        }

        public override byte ReadByte()
        {
            if (!CanRead(8))
            {
                IsError = true;

                return 0;
            }

            return ReadByteNoCheck();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override T ReadByteAsEnum<T>() => (T)Enum.ToObject(typeof(T), ReadByte());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBytes(Span<byte> data)
        {
            if (!CanRead(data.Length * 8))
            {
                IsError = true;
                return;
            }

            for (var i = 0; i < data.Length; i++)
            {
                data[i] = ReadByteNoCheck();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override byte[] ReadBytes(int byteCount)
        {
            if (byteCount < 0)
            {
                IsError = true;
                return Array.Empty<byte>();
            }

            if (!CanRead(byteCount))
            {
                IsError = true;
                return Array.Empty<byte>();
            }

            var result = new byte[byteCount];

            for (var i = 0; i < result.Length; i++)
            {
                result[i] = ReadByteNoCheck();
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override byte[] ReadBytes(uint byteCount) => ReadBytes((int)byteCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override T[] ReadArray<T>(Func<T> func1) => throw new NotImplementedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ReadBytesToString(int count)
        {
            if (count < 1024)
            {
                Span<byte> buffer = stackalloc byte[count];
                ReadBytes(buffer);
                return Convert.ToHexString(buffer);
            }
            else
            {
                var buffer = ArrayPool<byte>.Shared.Rent(count);
                try
                {
                    ReadBytes(buffer);
                    return Convert.ToHexString(buffer);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ReadFString()
        {
            var length = ReadInt32();

            if (length == 0)
            {
                return string.Empty;
            }

            var isUnicode = length < 0;
            if (isUnicode)
            {
                length *= -2;
            }

            if (length is > 256 or < 0)
            {
                IsError = true;
                return string.Empty;
            }

            Span<byte> bytes = stackalloc byte[length];
            ReadBytes(bytes);
            return isUnicode
                ? Encoding.Unicode.GetString(bytes[..^2])
                : Encoding.Default.GetString(bytes[..^1]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ReadGuid() => ReadBytesToString(16);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ReadGuid(int size) => ReadBytesToString(size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override uint ReadSerializedInt(int maxValue)
        {
            var value = 0;
            var count = 0;

            for (uint mask = 1; value + mask < maxValue; mask *= 2)
            {
                if (_position >= LastBit)
                {
                    IsError = true;
                    return 0;
                }

                value |= GetAsByte(_position++) << count++;
            }

            return (uint)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override short ReadInt16()
        {
            Span<byte> value = stackalloc byte[2];
            ReadBytes(value);
            return BinaryPrimitives.ReadInt16LittleEndian(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int ReadInt32()
        {
            Span<byte> value = stackalloc byte[4];
            ReadBytes(value);
            return BinaryPrimitives.ReadInt32LittleEndian(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool ReadInt32AsBoolean()
        {
            var i = ReadInt32();

            return i == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override long ReadInt64()
        {
            Span<byte> value = stackalloc byte[8];
            ReadBytes(value);
            return BinaryPrimitives.ReadInt64LittleEndian(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override uint ReadPackedUInt32()
        {
            var value = 0;
            byte count = 0;
            var remaining = true;

            while (remaining)
            {
                if (_position + 8 > LastBit)
                {
                    IsError = true;
                    return 0;
                }

                remaining = Bits[_position];

                value |= GetAsByte(_position + 1) << count;
                value |= GetAsByte(_position + 2) << (count + 1);
                value |= GetAsByte(_position + 3) << (count + 2);
                value |= GetAsByte(_position + 4) << (count + 3);
                value |= GetAsByte(_position + 5) << (count + 4);
                value |= GetAsByte(_position + 6) << (count + 5);
                value |= GetAsByte(_position + 7) << (count + 6);

                _position += 8;
                count += 7;
            }

            return (uint)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override FVector ReadPackedVector(int scaleFactor, int maxBits)
        {
            var bits = ReadSerializedInt(maxBits);
            var bias = 1 << ((int)bits + 1);
            var max = 1 << ((int)bits + 2);

            var dx = ReadSerializedInt(max);
            var dy = ReadSerializedInt(max);
            var dz = ReadSerializedInt(max);

            if (IsError)
            {
                return new FVector(0, 0, 0);
            }

            var x = (float)(dx - bias) / scaleFactor;
            var y = (float)(dy - bias) / scaleFactor;
            var z = (float)(dz - bias) / scaleFactor;

            return new FVector(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override FRotator ReadRotation()
        {
            float pitch = 0;
            float yaw = 0;
            float roll = 0;

            if (Bits[_position++]) // Pitch
            {
                pitch = ReadByte() * 360f / 256f;
            }

            if (Bits[_position++])
            {
                yaw = ReadByte() * 360f / 256f;
            }

            if (Bits[_position++])
            {
                roll = ReadByte() * 360f / 256f;
            }

            return IsError
                ? FRotator.Empty
                : new FRotator(pitch, yaw, roll);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override FRotator ReadRotationShort()
        {
            float pitch = 0;
            float yaw = 0;
            float roll = 0;

            if (Bits[_position++]) // Pitch
            {
                pitch = ReadUInt16() * 360 / 65536f;
            }

            if (Bits[_position++])
            {
                yaw = ReadUInt16() * 360 / 65536f;
            }

            if (Bits[_position++])
            {
                roll = ReadUInt16() * 360 / 65536f;
            }

            return IsError
                ? FRotator.Empty
                : new FRotator(pitch, yaw, roll);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override sbyte ReadSByte() => (sbyte)ReadByte();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float ReadSingle()
        {
            Span<byte> value = stackalloc byte[4];
            ReadBytes(value);
            return BinaryPrimitives.ReadSingleLittleEndian(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override (T, TU)[] ReadTupleArray<T, TU>(Func<T> func1, Func<TU> func2) =>
            throw new NotImplementedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ushort ReadUInt16()
        {
            Span<byte> value = stackalloc byte[2];
            ReadBytes(value);
            return BinaryPrimitives.ReadUInt16LittleEndian(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override uint ReadUInt32()
        {
            Span<byte> value = stackalloc byte[4];
            ReadBytes(value);
            return BinaryPrimitives.ReadUInt32LittleEndian(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool ReadUInt32AsBoolean() => ReadUInt32() == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override T ReadUInt32AsEnum<T>() => (T)Enum.ToObject(typeof(T), ReadUInt32());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ulong ReadUInt64()
        {
            Span<byte> value = stackalloc byte[8];
            ReadBytes(value);
            return BinaryPrimitives.ReadUInt64LittleEndian(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Seek(int offset, SeekOrigin seekOrigin = SeekOrigin.Begin)
        {
            if (offset < 0 || offset > LastBit || seekOrigin == SeekOrigin.Current && offset + _position > LastBit)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                    "Specified offset doesnt fit within the BitArray buffer");
            }

            _ = seekOrigin switch
            {
                SeekOrigin.Begin => _position = offset,
                SeekOrigin.End => _position = LastBit - offset,
                SeekOrigin.Current => _position += offset,
                _ => _position = offset
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SkipBytes(uint byteCount)
        {
            SkipBytes((int)byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SkipBytes(int byteCount)
        {
            Seek(byteCount * 8, SeekOrigin.Current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SkipBits(int numbits)
        {
            _position += numbits;

            if (numbits < 0 || _position > LastBit)
            {
                IsError = true;

                _position = LastBit;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Mark()
        {
            MarkPosition = _position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Pop()
        {
            _position = MarkPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetBitsLeft() => LastBit - _position;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AppendDataFromChecked(ReadOnlyMemory<bool> data)
        {
            AppendBits(data);
        }

        public override void Dispose()
        {
            DisposeBits();
            PositionQueues.Enqueue(_tempLastBit);
            PositionQueues.Enqueue(_tempPosition);
            GC.SuppressFinalize(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTempEnd(int totalBits, int index = 0)
        {
            _tempLastBit[index] = LastBit;
            _tempPosition[index] = _position + totalBits;
            LastBit = _position + totalBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RestoreTemp(int index = 0)
        {
            LastBit = _tempLastBit[index];
            _position = _tempPosition[index];

            /*
            _tempLastBit = 0;
            _tempPosition = 0;
            */

            IsError = false;
        }

        private static int[] GetPool() =>
            PositionQueues.TryDequeue(out var result)
                ? result
                : new int[8];

        protected bool* Bits;

        protected ReadOnlyMemory<bool> _items { get; set; }
        private IPinnedMemoryOwner<bool> _owner;

        public void SetBits(byte* ptr, int byteCount, int bitCount)
        {
            CreateBitArray(ptr, byteCount, bitCount);
            _position = 0;
        }

        public void DisposeBits()
        {
            _owner?.Dispose();
            _owner = null;
            _items = null;
            Bits = null;
        }

        private void CreateBitArray(byte* ptr, int byteCount, int totalBits)
        {
            _owner = PinnedMemoryPool<bool>.Shared.Rent(totalBits);
            _items = _owner.PinnedMemory.Memory;
            LastBit = totalBits;
            Bits = (bool*)_owner.PinnedMemory.Pointer;

            for (var i = 0; i < byteCount; i++)
            {
                var offset = i * 8;
                var deref = *(ptr + i);

                *(Bits + offset) = (deref & 0x01) == 0x01;
                *(Bits + offset + 1) = (deref & 0x02) == 0x02;
                *(Bits + offset + 2) = (deref & 0x04) == 0x04;
                *(Bits + offset + 3) = (deref & 0x08) == 0x08;
                *(Bits + offset + 4) = (deref & 0x10) == 0x10;
                *(Bits + offset + 5) = (deref & 0x20) == 0x20;
                *(Bits + offset + 6) = (deref & 0x40) == 0x40;
                *(Bits + offset + 7) = (deref & 0x80) == 0x80;
            }
        }

        private void AppendBits(ReadOnlyMemory<bool> after)
        {
            var newOwner = PinnedMemoryPool<bool>.Shared.Rent(after.Length + LastBit);
            var newMemory = newOwner.PinnedMemory.Memory;
            var oldLength = LastBit;

            //Copy old array
            _items.CopyTo(newMemory);

            DisposeBits(); //Get rid of old

            _items = newMemory;

            _owner = newOwner;
            Bits = (bool*)_owner.PinnedMemory.Pointer;

            var afterPin = after.Pin();

            Buffer.MemoryCopy(afterPin.Pointer, Bits + oldLength, after.Length, after.Length);

            afterPin.Dispose();

            LastBit = after.Length + LastBit;
        }

        protected byte GetAsByte(int index)
        {
            return (*(byte*)(Bits + index));
        }

        public UnrealNames ReadHardcodedName()
        {
            SkipBits(1);
            var nameIndex = EngineNetworkVersion < EngineNetworkVersionHistory.HistoryChannelNames
                ? ReadUInt32()
                : ReadPackedUInt32();
            return (UnrealNames)nameIndex;
        }
    }
}