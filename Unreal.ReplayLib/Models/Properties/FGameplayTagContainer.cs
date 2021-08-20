using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models.Properties
{
    public class FGameplayTagContainer : IProperty
    {
        public FGameplayTag[] Tags { get; private set; } = System.Array.Empty<FGameplayTag>();

        public void Serialize(NetBitReader reader)
        {
            var isEmpty = reader.ReadBit();

            if (isEmpty)
            {
                return;
            }

            var numTags = reader.ReadBitsToInt(7);

            Tags = new FGameplayTag[numTags];

            for (var i = 0; i < numTags; i++)
            {
                var tag = new FGameplayTag();
                tag.Serialize(reader);

                Tags[i] = tag;
            }
        }

        public void UpdateTags(NetFieldExportGroup networkGameplayTagNode)
        {
            foreach (var t in Tags)
            {
                t.UpdateTagName(networkGameplayTagNode);
            }
        }
    }
}