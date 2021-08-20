using System.Collections.Generic;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.Models
{
    /// <summary>
    /// Base class of communication channels.
    /// see https://github.com/EpicGames/UnrealEngine/blob/release/Engine/Source/Runtime/Engine/Classes/Engine/Channel.h
    /// </summary>
    public class UChannel
    {
        public string ChannelName { get; set; }
        public uint ChannelIndex { get; set; }
        public ChannelType ChannelType { get; set; }
        public bool Broken { get; set; }
        public Actor Actor { get; set; }
        public bool? IgnoreChannel { get; set; }
        public HashSet<string> Group { get; set; } = new();
    }
}