namespace Unreal.ReplayLib.Models
{
    public class NetFieldExport
    {
        public bool IsExported { get; set; }
        public uint Handle { get; set; }
        public uint CompatibleChecksum { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool Incompatible { get; set; }
        internal string CleanedName { get; set; }
        internal int PropertyId { get; set; } = -1;
    }
}