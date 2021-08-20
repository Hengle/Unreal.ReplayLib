using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.Models.Properties
{
    public class FStaticName : IProperty
    {
        public string Value { get; private set; }

        public void Serialize(NetBitReader reader)
        {
            var isHardcoded = reader.ReadBoolean();

            if (isHardcoded)
            {
                uint nameIndex;
                if (reader.EngineNetworkVersion < EngineNetworkVersionHistory.HistoryChannelNames)
                {
                    nameIndex = reader.ReadUInt32();
                }
                else
                {
                    nameIndex = reader.ReadPackedUInt32();
                }

                Value = UnrealNameConstants.Names[nameIndex];
                return;
            }

            var inString = reader.ReadFString();
            var inNumber = reader.ReadInt32();

            Value = inString;
        }

        public override string ToString() => Value;
    }
}