using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Unreal.ReplayLib.Memory;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.IO
{
    public sealed unsafe class UnrealBinaryReader : FArchive
    {
        private readonly BinaryReader _reader;
        public Stream BaseStream => _reader.BaseStream;

        public override int Position
        {
            get => (int) BaseStream.Position;
            protected set => Seek(value);
        }
        private IPinnedMemoryOwner<byte> _owner;
        public byte* BasePointer => (byte*)_owner.PinnedMemory.Pointer;

        public UnrealBinaryReader(Stream input) => _reader = new BinaryReader(input);

        public UnrealBinaryReader(int size)
        {
            CreateMemory(size);
            _reader = new BinaryReader(new UnmanagedMemoryStream((byte*) _owner.PinnedMemory.Pointer, size, size,
                FileAccess.ReadWrite));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool AtEnd() => _reader.BaseStream.Position >= _reader.BaseStream.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool CanRead(int count) => _reader.BaseStream.Position + count < _reader.BaseStream.Length;

        public override void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _owner?.Dispose();
                _reader.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryBuffer GetMemoryBuffer(int count)
        {
            //Removes the need for a MemoryPool Rent
            if(_owner != null)
            {
                var buffer = new MemoryBuffer(BasePointer + Position, count);

                _reader.BaseStream.Seek(count, SeekOrigin.Current);

                return buffer;
            }
            var stream = new MemoryBuffer(count);

            _reader.Read(stream.Memory.Span[..count]);

            return stream;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateMemory(int count)
        {
            if (_owner != null)
            {
                throw new InvalidOperationException("Memory object already created");
            }
            _owner = PinnedMemoryPool<byte>.Shared.Rent(count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override T[] ReadArray<T>(Func<T> func1)
        {
            var count = ReadUInt32();
            var arr = new T[count];
            for (var i = 0; i < count; i++)
            {
                arr[i] = func1.Invoke();
            }

            return arr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool ReadBoolean() => _reader.ReadBoolean();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override byte ReadByte() => _reader.ReadByte();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override T ReadByteAsEnum<T>() => (T) Enum.ToObject(typeof(T), ReadByte());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override byte[] ReadBytes(int byteCount) => _reader.ReadBytes(byteCount);
        
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public ReadOnlySpan<byte> PeekBytes(int byteCount) => BaseStream.ReadAsync() .rea(byteCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override byte[] ReadBytes(uint byteCount) => _reader.ReadBytes((int) byteCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ReadBytesToString(int count)
        {
            if (count < 1024)
            {
                Span<byte> buffer = stackalloc byte[count];
                _reader.Read(buffer);
                return Convert.ToHexString(buffer);
            }
            else
            {
                var buffer = ArrayPool<byte>.Shared.Rent(count);
                try
                {
                    _reader.Read(buffer);
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
            var encoding = isUnicode ? Encoding.Unicode : Encoding.Default;
            if (isUnicode)
            {
                length *= -2;
            }

            Span<byte> buffer = stackalloc byte[length];
            _reader.Read(buffer);
            return encoding.GetString(buffer).Trim(' ', '\0');
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SkipFString()
        {
            var length = ReadInt32();
            length = length < 0 ? -2 * length : length;
            SkipBytes(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ReadGuid() => ReadBytesToString(16);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ReadGuid(int size) => ReadBytesToString(size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override short ReadInt16() => _reader.ReadInt16();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int ReadInt32() => _reader.ReadInt32();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool ReadInt32AsBoolean() => _reader.ReadInt32() == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override long ReadInt64() => _reader.ReadInt64();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override uint ReadPackedUInt32()
        {
            uint value = 0;
            byte count = 0;
            var remaining = true;

            while (remaining)
            {
                var nextByte = ReadByte();
                remaining = (nextByte & 1) == 1; // Check 1 bit to see if theres more after this
                nextByte >>= 1; // Shift to get actual 7 bit value
                value += (uint) nextByte << (7 * count++); // Add to total value
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override sbyte ReadSByte() => _reader.ReadSByte();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float ReadSingle() => _reader.ReadSingle();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override (T, TU)[] ReadTupleArray<T, TU>(Func<T> func1, Func<TU> func2)
        {
            var count = ReadUInt32();
            var arr = new (T, TU)[count];
            for (var i = 0; i < count; i++)
            {
                arr[i] = (func1.Invoke(), func2.Invoke());
            }

            return arr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ushort ReadUInt16() => _reader.ReadUInt16();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override uint ReadUInt32() => _reader.ReadUInt32();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool ReadUInt32AsBoolean() => ReadUInt32() == 1u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override T ReadUInt32AsEnum<T>() => (T) Enum.ToObject(typeof(T), ReadUInt32());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ulong ReadUInt64() => _reader.ReadUInt64();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Seek(int offset, SeekOrigin seekOrigin = SeekOrigin.Begin) => _reader.BaseStream.Seek(offset, seekOrigin);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SkipBytes(uint byteCount) => _reader.BaseStream.Seek(byteCount, SeekOrigin.Current);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SkipBytes(int byteCount) => _reader.BaseStream.Seek(byteCount, SeekOrigin.Current);

        public FVector ReadQuantizedVector()
        {
            var a = ReadInt32();
            var dx = ReadInt32();
            var dy = ReadInt32();
            var dz = ReadInt32();
            var bias = 1 << (a + 1);
            var max = 1 << (a + 2);
            var scaleFactor = 1;
            var x = (float)(dx - bias) / scaleFactor;
            var y = (float)(dy - bias) / scaleFactor;
            var z = (float)(dz - bias) / scaleFactor;
            return new FVector(x, y, z);
        }
    }
}