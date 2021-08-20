namespace Unreal.ReplayLib.Models
{
    /// <summary>
    /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Engine/Classes/Engine/PackageMapClient.h#L124
    /// </summary>
    public class NetFieldExportGroup
    {
        public string PathName { get; set; }
        public string CleanedPath { get; set; }
        public uint PathNameIndex { get; set; }
        public uint NetFieldExportsLength { get; set; }
        public NetFieldExport[] NetFieldExports { get; set; }
        public int GroupId { get; set; } = -1;
        public bool IsValidIndex(uint handle) => handle < NetFieldExportsLength;
    }
}