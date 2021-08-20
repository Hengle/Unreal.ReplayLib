namespace Unreal.ReplayLib.Models
{
    public abstract class NetFieldExportGroupBase
    {
        public Actor ChannelActor { get; protected internal set; }
        public object Clone() => MemberwiseClone();
    }
}