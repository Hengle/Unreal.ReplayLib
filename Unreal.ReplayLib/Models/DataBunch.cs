using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.Models
{
    /// <summary>
    /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Engine/Public/Net/DataBunch.h#L112
    /// </summary>
    public class DataBunch
    {
        public DataBunch()
        {
        }

        public DataBunch(DataBunch inBunch)
        {
            Archive = inBunch.Archive;
            PacketId = inBunch.PacketId;
            ChIndex = inBunch.ChIndex;
            ChType = inBunch.ChType;
            ChName = inBunch.ChName;
            ChSequence = inBunch.ChSequence;
            BOpen = inBunch.BOpen;
            BClose = inBunch.BClose;
            BDormant = inBunch.BDormant;
            BIsReplicationPaused = inBunch.BIsReplicationPaused;
            BReliable = inBunch.BReliable;
            BPartial = inBunch.BPartial;
            BPartialInitial = inBunch.BPartialInitial;
            BPartialFinal = inBunch.BPartialFinal;
            BHasPackageMapExports = inBunch.BHasPackageMapExports;
            BHasMustBeMappedGuids = inBunch.BHasMustBeMappedGuids;
            BIgnoreRpcs = inBunch.BIgnoreRpcs;
            CloseReason = inBunch.CloseReason;
        }

        public NetBitReader Archive { get; set; }

        public int PacketId { get; set; }

        //FInBunch* Next;
        //UNetConnection* Connection;
        public uint ChIndex { get; set; }

        // UE_DEPRECATED(4.22, "ChType deprecated in favor of ChName.")
        public ChannelType ChType { get; set; }

        // FName
        public string ChName { get; set; }
        public int ChSequence { get; set; }
        public bool BOpen { get; set; }

        public bool BClose { get; set; }

        // UE_DEPRECATED(4.22, "bDormant is deprecated in favor of CloseReason")
        public bool BDormant { get; set; } // Close, but go dormant
        public bool BIsReplicationPaused { get; set; } // Replication on this channel is being paused by the server
        public bool BReliable { get; set; }
        public bool BPartial { get; set; } // Not a complete bunch
        public bool BPartialInitial { get; set; } // The first bunch of a partial bunch
        public bool BPartialFinal { get; set; } // The final bunch of a partial bunch
        public bool BHasPackageMapExports { get; set; } // This bunch has networkGUID name/id pairs

        /// <summary>
        /// This bunch has guids that must be mapped before we can process this bunch
        /// </summary>
        public bool BHasMustBeMappedGuids { get; set; }

        public bool BIgnoreRpcs { get; set; }
        public ChannelCloseReason CloseReason { get; set; }
    }
}