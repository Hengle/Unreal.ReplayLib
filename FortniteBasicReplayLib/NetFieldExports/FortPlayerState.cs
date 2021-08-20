using Unreal.ReplayLib.Attributes;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;

namespace FortniteBasicReplayLib.NetFieldExports
{
    [NetFieldExportGroup("/Script/FortniteGame.FortPlayerStateAthena")]
    public class FortPlayerState : NetFieldExportGroupBase
    {
        [NetFieldExport("UniqueId", RepLayoutCmdType.PropertyNetId)]
        public string UniqueId { get; set; }

        [NetFieldExport("Ping", RepLayoutCmdType.PropertyByte)]
        public byte? Ping { get; set; }
    }
}