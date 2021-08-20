using System;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class NetFieldExportAttribute : Attribute
    {
        public RepLayoutCmdType Type { get; set; }

        public string Name { get; set; }

        public NetFieldExportAttribute(string name, RepLayoutCmdType type)
        {
            Name = name;
            Type = type;
        }
    }
}