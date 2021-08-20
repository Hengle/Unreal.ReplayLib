using System.Reflection;
using Unreal.ReplayLib.Attributes;

namespace Unreal.ReplayLib.NetFields
{
    public sealed class NetRpcFieldInfo
    {
        public NetFieldExportRpcPropertyAttribute Attribute { get; set; }
        public PropertyInfo PropertyInfo { get; set; }
        public bool IsCustomStructure { get; set; }
    }
}