using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models.Properties
{
    public class FGameplayTag : IProperty
    {
        public string TagName { get; set; }
        public uint? TagIndex { get; private set; }

        public void Serialize(NetBitReader reader)
        {
            TagIndex = reader.ReadPackedUInt32();
        }

        public override string ToString() => TagName ?? TagIndex.ToString();

        public void UpdateTagName(NetFieldExportGroup networkGameplayTagNode)
        {
            if (networkGameplayTagNode == null || TagIndex > networkGameplayTagNode.NetFieldExportsLength)
            {
                return;
            }
            TagName = networkGameplayTagNode.NetFieldExports[(int) TagIndex]?.Name;
        }
    }
}