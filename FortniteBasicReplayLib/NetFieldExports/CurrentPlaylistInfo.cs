using Unreal.ReplayLib.Attributes;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Properties;

namespace FortniteBasicReplayLib.NetFieldExports
{
    [NetFieldExportGroup("CurrentPlaylistInfo")]
    public class CurrentPlaylistInfo : NetFieldExportGroupBase, IProperty
    {
        public NetworkGuid Id { get; private set; }

        public void Serialize(NetBitReader reader)
        {
            reader.SkipBits(2);

            Id = new NetworkGuid
            {
                Value = reader.ReadPackedUInt32()
            };

            reader.SkipBits(31);
        }
    }
}