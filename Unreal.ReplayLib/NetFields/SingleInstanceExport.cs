using Unreal.ReplayLib.Extensions;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.NetFields
{
    public sealed class SingleInstanceExport
    {
        internal NetFieldExportGroupBase Instance { get; set; }
        internal FastClearArray<NetFieldInfo> ChangedProperties { get; set; }
    }
}