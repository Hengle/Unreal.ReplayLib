using System.Collections.Generic;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.Models
{
    //Throws unknown handles here instead of throwing warnings 
    public abstract class HandleNetFieldExportGroup : NetFieldExportGroupBase
    {
        public abstract RepLayoutCmdType Type { get; protected set; }
        public Dictionary<uint, object> UnknownHandles = new();
    }
}