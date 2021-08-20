using System.Collections.Generic;

namespace Unreal.ReplayLib.Models
{
    public class Replay
    {
        public ReplayInfo Info { get; set; }
        public ReplayHeader Header { get; set; } = new();
        public List<EventInfo> Events { get; } = new();
        public Dictionary<string, HashSet<string>> ExportGroupFieldDict { get; } = new();
        public Dictionary<uint, string> NetGuidToPathName { get; set; } = new();
        public long ParseTime { get; set; }
    }
}