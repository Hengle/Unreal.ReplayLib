using FortniteBasicReplayLib.Models;
using FortniteBasicReplayLib.NetFieldExports;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.NetFields;

namespace FortniteBasicReplayLib
{
    public class BasicFortniteReplayReader : ReplayReader<FortniteReplay>
    {
        private static readonly NetFieldExportGroupInfo ExportGroups = NetFieldExportGroupInfo.FromTypes(
            new[]
            {
                typeof(CurrentPlaylistInfo),
                typeof(FortPlayerState),
                typeof(GameStateC),
                typeof(GameStateCache),
                typeof(ReplayPc),
            });

        public BasicFortniteReplayReader(ILogger logger = null) : base(logger, ExportGroups)
        {
        }

        protected override bool ShouldContinueParsing() =>
            Replay.Match.Branch == null
            || Replay.Match.MatchId == null
            || Replay.Match.MatchStart == null
            || Replay.Match.PlayerId == null
            || Replay.Match.PlaylistId == null;

        protected override void OnFinishParse()
        {
            Replay.Match.Branch = Replay.Header.Branch;
            Replay.Match.ParseTime = Replay.ParseTime;
        }

        protected override void OnExportRead(uint channelId, NetFieldExportGroupBase exportGroup)
        {
            switch (exportGroup)
            {
                case CurrentPlaylistInfo playlistInfo:
                    Replay.UpdatePlaylistInfo(playlistInfo);
                    break;
                case GameStateC gameState:
                    Replay.UpdateGameState(gameState);
                    break;
                case FortPlayerState playerState:
                    Replay.UpdatePlayerState(channelId, playerState);
                    break;
            }
        }
    }
}