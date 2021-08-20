using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models.Properties
{
    public class NetworkGuid : IProperty
    {
        public uint Value { get; set; }

        public bool IsValid() => Value > 0;

        public bool IsDynamic() => Value > 0 && (Value & 1) != 1;

        public bool IsDefault() => Value == 1;

        public bool IsStatic() => (Value & 1) == 1;

        public void Serialize(NetBitReader reader) => Value = reader.SerializePropertyObject();

        public override string ToString() => Value.ToString();
    }
}