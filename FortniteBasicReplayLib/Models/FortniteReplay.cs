using System.Collections.Generic;
using FortniteBasicReplayLib.NetFieldExports;
using Unreal.ReplayLib.Models;

namespace FortniteBasicReplayLib.Models
{
    public class FortniteReplay : Replay
    {
        private readonly Dictionary<uint, Player> _channelIdToPlayerDict = new();
        public Match Match { get; } = new();

        internal void UpdatePlaylistInfo(CurrentPlaylistInfo playlistInfo)
        {
            if (NetGuidToPathName.TryGetValue(playlistInfo.Id.Value, out var playlistId))
            {
                Match.PlaylistId = playlistId;
            }
        }

        internal void UpdateGameState(GameStateC gameState)
        {
            Match.MatchId = gameState.GameSessionId ?? Match.MatchId;
            Match.MatchStart = gameState.UtcTimeStartedMatch?.Time ?? Match.MatchStart;
        }

        internal void UpdatePlayerState(uint channelIndex, FortPlayerState playerState)
        {
            var isNewPlayer = !_channelIdToPlayerDict.TryGetValue(channelIndex, out var newPlayer);
            if (isNewPlayer)
            {
                newPlayer = new Player();
                _channelIdToPlayerDict.TryAdd(channelIndex, newPlayer);
            }

            newPlayer.EpicId = playerState.UniqueId ?? newPlayer.EpicId;
            newPlayer.IsPlayersReplay = playerState.Ping > 0 || newPlayer.IsPlayersReplay;

            if (newPlayer.IsPlayersReplay)
            {
                Match.PlayerId ??= newPlayer.EpicId;
            }
        }
    }
}