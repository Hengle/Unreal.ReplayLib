using System;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.IO
{
    public abstract class FBitArchive : FArchive
    {
        public abstract bool PeekBit();
        public abstract byte PeekByte();
        public abstract bool ReadBit();
        public abstract ReadOnlyMemory<bool> ReadBits(int bitCount);
        public abstract ReadOnlyMemory<bool> ReadBits(uint bitCount);
        public abstract uint ReadSerializedInt(int maxValue);
        public abstract FVector ReadPackedVector(int scaleFactor, int maxBits);
        public abstract FRotator ReadRotation();
        public abstract FRotator ReadRotationShort();
        public abstract void Mark();
        public abstract void Pop();
        public abstract int GetBitsLeft();
        public abstract void AppendDataFromChecked(ReadOnlyMemory<bool> data);
        public abstract void SkipBits(int bitCount);
    }
}