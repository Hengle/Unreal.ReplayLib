using System;

namespace Unreal.ReplayLib.Models.Enums
{
    [Flags]
    public enum ExportFlags
    {
        None = 0,
        BHasPath = (1 << 0),
        BNoLoad = (1 << 1),
        BHasNetworkChecksum = (1 << 2)
    }
}