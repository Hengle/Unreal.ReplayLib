using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models.Properties
{
    public class FTransform : IProperty
    {
        public FQuat Rotation { get; set; }
        public FVector Scale3D { get; set; }
        public FVector Translation { get; set; }

        public void Serialize(NetBitReader reader)
        {
            Rotation = new FQuat();
            Rotation.Serialize(reader);

            Scale3D = new FVector();
            Scale3D.Serialize(reader);

            Translation = new FVector();
            Translation.Serialize(reader);
        }

        public void Serialize(FArchive reader)
        {
            Rotation = new FQuat
            {
                W = reader.ReadSingle(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Z = reader.ReadSingle()
            };
            Scale3D = new FVector
            {
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Z = reader.ReadSingle()
            };
            Translation = new FVector
            {
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Z = reader.ReadSingle()
            };
        }
    }
}