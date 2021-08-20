using System.Collections.Generic;
using System.Linq;
using Unreal.ReplayLib.Attributes;
using Unreal.ReplayLib.Models.Enums;
using Unreal.ReplayLib.Models.Properties;

namespace Unreal.ReplayLib.Models
{
    [NetFieldExportGroup("DebuggingExportGroup")]
    public class DebuggingExportGroup : NetFieldExportGroupBase
    {
        public NetFieldExportGroup ExportGroup { get; set; }

        public Dictionary<uint, string> HandleNames => ExportGroup?.NetFieldExports.Where(x => x != null)
            .ToDictionary(x => x.Handle, x => x.Name);


        [NetFieldExport("Handles", RepLayoutCmdType.Debug)]
        public Dictionary<uint, DebuggingObject> Handles { get; set; } = new();

        public override string ToString() => ExportGroup?.PathName;
    }
}