namespace Unreal.ReplayLib.Models
{
    public class DebuggingHandle
    {
        public uint NumBits { get; set; }
        public uint Handle { get; set; }

        public override string ToString() => $"Handle: {Handle} NumBits: {NumBits}";
    }
}