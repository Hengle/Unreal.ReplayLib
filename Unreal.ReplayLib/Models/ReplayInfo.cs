using System;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.Models
{
    /// <summary>
    /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/NetworkReplayStreaming/LocalFileNetworkReplayStreaming/Public/LocalFileNetworkReplayStreaming.h#L88
    /// </summary>
    public class ReplayInfo
    {
        public uint LengthInMs { get; set; }
        public uint NetworkVersion { get; set; }
        public uint Changelist { get; set; }
        public string FriendlyName { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public bool IsLive { get; set; }
        public bool IsCompressed { get; set; }
        public bool Encrypted { get; set; }
        public byte[] EncryptionKey { get; set; }
        public ReplayVersionHistory FileVersion { get; set; }
        public bool ValidEncryptionStatus => !Encrypted || EncryptionKey.Length > 0;
    }
}