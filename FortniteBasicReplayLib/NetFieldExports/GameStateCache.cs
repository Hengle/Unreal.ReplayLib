using Unreal.ReplayLib.Attributes;

namespace FortniteBasicReplayLib.NetFieldExports
{
    [NetFieldExportRpc("Athena_GameState_C_ClassNetCache")]
    public class GameStateCache
    {
        [NetFieldExportRpcProperty("CurrentPlaylistInfo", "CurrentPlaylistInfo", customStructure: true)]
        public CurrentPlaylistInfo CurrentPlaylistInfo { get; set; }
    }
}