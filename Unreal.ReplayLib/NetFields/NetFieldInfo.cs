using System;
using System.Reflection;
using Unreal.ReplayLib.Attributes;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.NetFields
{
    public sealed class NetFieldInfo
    {
        public NetFieldExportAttribute Attribute { get; set; }
        public PropertyInfo PropertyInfo { get; set; }
        public object DefaultValue { get; set; }
        public int ElementTypeId { get; set; }
        public Action<NetFieldExportGroupBase, object> SetMethod { get; set; }
    }
}