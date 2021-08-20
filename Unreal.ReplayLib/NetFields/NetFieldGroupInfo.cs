using System;
using System.Collections.Generic;
using Unreal.ReplayLib.Attributes;
using Unreal.ReplayLib.Extensions;

namespace Unreal.ReplayLib.NetFields
{
    public sealed class NetFieldGroupInfo
    {
        public NetFieldExportGroupAttribute Attribute { get; set; }
        public Type Type { get; set; }
        public int TypeId { get; set; }

        public bool UsesHandles { get; set; }
        public bool SingleInstance { get; set; }

        public KeyList<string, NetFieldInfo> Properties { get; set; } = new();
        public Dictionary<uint, NetFieldInfo> HandleProperties { get; set; } = new();
    }
}