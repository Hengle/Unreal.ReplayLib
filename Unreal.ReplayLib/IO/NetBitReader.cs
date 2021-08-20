using System;
using System.IO;
using System.Runtime.CompilerServices;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;
using Unreal.ReplayLib.Models.Properties;

namespace Unreal.ReplayLib.IO
{
    public unsafe class NetBitReader : BitReader
    {
        public NetBitReader()
        {
        }

        public NetBitReader(byte* buffer, int byteCount, int bitCount) : base(buffer, byteCount, bitCount)
        {
        }

        private NetBitReader(bool* boolPtr, int bitCount) : base(boolPtr, bitCount)
        {
        }

        public NetBitReader GetNetBitReader(int bitCount)
        {
            var reader = new NetBitReader(Bits + Position, bitCount);
            reader._items = _items.Slice(Position, bitCount);

            Position += bitCount;

            return reader;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SerializePropertyInt() => ReadInt32();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint SerializePropertyUInt32() => ReadUInt32();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint SerializePropertyUInt16() => ReadUInt16();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong SerializePropertyUInt64() => ReadUInt64();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SerializePropertyFloat() => ReadSingle();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string SerializePropertyName()
        {
            var isHardcoded = ReadBoolean();
            if (isHardcoded)
            {
                var nameIndex = EngineNetworkVersion < EngineNetworkVersionHistory.HistoryChannelNames
                    ? ReadUInt32()
                    : ReadPackedUInt32();

                return ((UnrealNames)nameIndex).ToString();
            }

            var inString = ReadFString();
            var inNumber = ReadInt32();

            return inString;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string SerializePropertyString()
        {
            if (GetBitsLeft() == 32)
            {
                Seek(32, SeekOrigin.Current);
                return string.Empty;
            }

            return ReadFString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FRepMovement SerializeRepMovement(
            VectorQuantization locationQuantizationLevel = VectorQuantization.RoundTwoDecimals,
            RotatorQuantization rotationQuantizationLevel = RotatorQuantization.ByteComponents,
            VectorQuantization velocityQuantizationLevel = VectorQuantization.RoundWholeNumber)
        {
            var repMovement = new FRepMovement();
            if (!CanRead(2))
            {
                IsError = true;

                return repMovement;
            }

            repMovement.BSimulatedPhysicSleep = Bits[Position++];
            repMovement.BRepPhysics = Bits[Position++];

            repMovement.Location = SerializeQuantizedVector(locationQuantizationLevel);

            repMovement.Rotation = rotationQuantizationLevel switch
            {
                RotatorQuantization.ByteComponents => ReadRotation(),
                RotatorQuantization.ShortComponents => ReadRotationShort(),
                _ => repMovement.Rotation
            };

            // repMovement.LinearVelocity = SerializeQuantizedVector(velocityQuantizationLevel);
            //
            // if (repMovement.BRepPhysics)
            // {
            //     repMovement.AngularVelocity = SerializeQuantizedVector(velocityQuantizationLevel);
            // }

            return repMovement;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FVector2D SerializeVector2D()
        {
            var x = ReadSingle();
            var y = ReadSingle();
            return new FVector2D(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FVector SerializePropertyVector() => new(ReadSingle(), ReadSingle(), ReadSingle());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FVector SerializePropertyVectorNormal() =>
            new(ReadFixedCompressedFloat(1, 16), ReadFixedCompressedFloat(1, 16),
                ReadFixedCompressedFloat(1, 16));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FVector SerializePropertyVector10() => ReadPackedVector(10, 24);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FVector SerializePropertyVector100() => ReadPackedVector(100, 30);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FVector SerializePropertyQuantizeVector() => ReadPackedVector(1, 20);

        public void SerializPropertyPlane()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFixedCompressedFloat(int maxValue, int numBits)
        {
            var maxBitValue = (1 << (numBits - 1)) - 1;
            var bias = 1 << (numBits - 1);
            var serIntMax = 1 << (numBits - 0);
            //int maxDelta = (1 << (numBits - 0)) -1;

            var delta = ReadSerializedInt(serIntMax);
            float unscaledValue = unchecked((int)delta) - bias;

            if (maxValue > maxBitValue)
            {
                var invScale = maxValue / (float)maxBitValue;

                return unscaledValue * invScale;
            }
            else
            {
                var scale = maxBitValue / (float)maxValue;
                var invScale = 1f / scale;

                return unscaledValue * invScale;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FRotator? SerializePropertyRotator() => ReadRotationShort();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SerializePropertyByte(int enumMaxValue)
        {
            //Ar.SerializeBits( Data, Enum ? FMath::CeilLogTwo(Enum->GetMaxEnumValue()) : 8 );

            var log2 = Math.Log2(enumMaxValue);
            return ReadBitsToInt(enumMaxValue > 0 ? (int)Math.Ceiling(log2) : 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte SerializePropertyByte() => (byte)SerializePropertyByte(-1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SerializeEnum(int bits) => ReadBitsToInt(bits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SerializeEnum() => ReadBitsToInt(GetBitsLeft());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SerializePropertyBool() => ReadBit();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SerializePropertyNativeBool() => ReadBit();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SerializePropertyEnum(int enumMaxValue) => ReadBitsToInt((int)CeilLogTwo64((ulong)enumMaxValue));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint SerializePropertyObject()
        {
            //InternalLoadObject(); // TODO make available in archive

            var netGuid = new NetworkGuid
            {
                Value = ReadPackedUInt32()
            };

            return netGuid.Value;

            //if (!netGuid.IsValid())
            //{
            //    return;
            //}

            //if (netGuid.IsDefault() || exportGUIDs)
            //{
            //    var flags = archive.ReadByteAsEnum<ExportFlags>();

            //    // outerguid
            //    if (flags == ExportFlags.bHasPath || flags == ExportFlags.bHasPathAndNetWorkChecksum || flags == ExportFlags.All)
            //    {
            //        var outerGuid = InternalLoadObject(archive, true); // TODO: archetype?

            //        var pathName = archive.ReadFString();

            //        if (!NetGuidCache.ContainsKey(netGuid.Value))
            //        {
            //            NetGuidCache.Add(netGuid.Value, pathName);
            //        }

            //        if (flags >= ExportFlags.bHasNetworkChecksum)
            //        {
            //            var networkChecksum = archive.ReadUInt32();
            //        }

            //        return netGuid;
            //    }
            //}

            //return netGuid;

            //UObject* Object = GetObjectPropertyValue(Data);
            //bool Result = Map->SerializeObject(Ar, PropertyClass, Object);
            //SetObjectPropertyValue(Data, Object);
            //return Result;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FVector SerializeQuantizedVector(VectorQuantization quantizationLevel)
        {
            return quantizationLevel switch
            {
                VectorQuantization.RoundTwoDecimals => ReadPackedVector(100, 30),
                VectorQuantization.RoundOneDecimal => ReadPackedVector(10, 27),
                _ => ReadPackedVector(1, 24)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string SerializePropertyNetId()
        {
            if (GetBitsLeft() == 32)
            {
                Seek(32, SeekOrigin.Current);
                return string.Empty;
            }

            // Use highest value for type for other (out of engine) oss type 
            const byte typeHashOther = 31;

            var encodingFlags = ReadByteAsEnum<UniqueIdEncodingFlags>();
            var encoded = false;
            if ((encodingFlags & UniqueIdEncodingFlags.IsEncoded) == UniqueIdEncodingFlags.IsEncoded)
            {
                encoded = true;
                if ((encodingFlags & UniqueIdEncodingFlags.IsEmpty) == UniqueIdEncodingFlags.IsEmpty)
                {
                    // empty cleared out unique id
                    return string.Empty;
                }
            }

            // Non empty and hex encoded
            var typeHash = (int)(encodingFlags & UniqueIdEncodingFlags.TypeMask) >> 3;
            if (typeHash == 0)
            {
                // If no type was encoded, assume default
                //TypeHash = UOnlineEngineInterface::Get()->GetReplicationHashForSubsystem(UOnlineEngineInterface::Get()->GetDefaultOnlineSubsystemName());
                return "NULL";
            }

            var bValidTypeHash = typeHash != 0;
            if (typeHash == typeHashOther)
            {
                var typeString = ReadFString();
                if (typeString == UnrealNameConstants.Names[(int)UnrealNames.None])
                {
                    bValidTypeHash = false;
                }
            }

            if (bValidTypeHash)
            {
                if (encoded)
                {
                    var encodedSize = ReadByte();
                    return ReadBytesToString(encodedSize);
                }
                else
                {
                    return ReadFString();
                }
            }

            return string.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong FloorLog2_64(ulong value)
        {
            ulong pos = 0;
            if (value >= 1ul << 32)
            {
                value >>= 32;
                pos += 32;
            }

            if (value >= 1ul << 16)
            {
                value >>= 16;
                pos += 16;
            }

            if (value >= 1ul << 8)
            {
                value >>= 8;
                pos += 8;
            }

            if (value >= 1ul << 4)
            {
                value >>= 4;
                pos += 4;
            }

            if (value >= 1ul << 2)
            {
                value >>= 2;
                pos += 2;
            }

            if (value >= 1ul << 1)
            {
                pos += 1;
            }

            return value == 0 ? 0 : pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CountLeadingZeros64(ulong value)
        {
            if (value == 0)
            {
                return 64;
            }

            return 63 - FloorLog2_64(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CeilLogTwo64(ulong arg)
        {
            var bitmask = CountLeadingZeros64(arg) << 57 >> 63;
            return (64 - CountLeadingZeros64(arg - 1)) & ~bitmask;
        }
    }
}