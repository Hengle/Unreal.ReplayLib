using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.Models.Properties
{
    public class FText : IProperty
    {
        public string Namespace { get; set; }
        public string Key { get; set; }
        public string Text { get; set; }

        public void Serialize(NetBitReader reader)
        {
            var flags = reader.ReadInt32();
            var historyType = reader.ReadByteAsEnum<ETextHistoryType>();

            switch (historyType)
            {
                case ETextHistoryType.Base:
                    Namespace = reader.ReadFString();
                    Key = reader.ReadFString();
                    Text = reader.ReadFString();
                    break;
            }
        }

        public override string ToString() => Text;
    }
}