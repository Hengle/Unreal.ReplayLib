namespace Unreal.ReplayLib.Models
{
    public class NetDeltaUpdate
    {
        public bool Deleted { get; internal set; }
        public int ElementIndex { get; internal set; }
        public NetFieldExportGroupBase Export { get; internal set; }
        public uint ChannelIndex { get; internal set; }
        public FFastArraySerializerHeader Header { get; internal set; }

        //Not sure if I will need these for later
        public NetFieldExportGroup ExportGroup { get; internal set; }
        public NetFieldExportGroup PropertyExport { get; internal set; }
        public uint Handle { get; internal set; }
    }
}