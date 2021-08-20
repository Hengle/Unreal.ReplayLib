using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models.Properties
{
    public class FQuat : IProperty
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }

        //bool FQuat::NetSerialize(FArchive& Ar, class UPackageMap*, bool& bOutSuccess)
        public void Serialize(NetBitReader reader)
        {
            X = reader.ReadSingle();
            Y = reader.ReadSingle();
            Z = reader.ReadSingle();

            var xyzMagSquared = X * X + Y * Y + Z * Z;
            var wSquared = 1.0f - xyzMagSquared;
        }
    }
}