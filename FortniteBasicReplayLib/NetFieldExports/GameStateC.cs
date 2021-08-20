using Unreal.ReplayLib.Attributes;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;
using Unreal.ReplayLib.Models.Properties;

namespace FortniteBasicReplayLib.NetFieldExports
{
    [NetFieldExportGroup("/Game/Athena/Athena_GameState.Athena_GameState_C")]
    public class GameStateC : NetFieldExportGroupBase
    {
        [NetFieldExport("GameSessionId", RepLayoutCmdType.PropertyString)]
        public string GameSessionId { get; set; }

        [NetFieldExport("UtcTimeStartedMatch", RepLayoutCmdType.Property)]
        public FDateTime UtcTimeStartedMatch { get; set; }
    }
}