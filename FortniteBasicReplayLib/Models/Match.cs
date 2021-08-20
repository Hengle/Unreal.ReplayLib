using System;

namespace FortniteBasicReplayLib.Models
{
    public class Match
    {
        public string MatchId { get; set; }
        public string PlayerId { get; set; }
        public string PlaylistId { get; set; }
        public DateTimeOffset? MatchStart { get; set; }
        public long ParseTime { get; set; }
        public string Branch { get; set; }
    }
}