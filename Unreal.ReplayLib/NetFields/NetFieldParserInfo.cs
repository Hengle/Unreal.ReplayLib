using System;
using System.Collections.Generic;
using Unreal.ReplayLib.Extensions;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.NetFields
{
    /// <summary>
    /// Holds type info for assembly
    /// </summary>
    public sealed class NetFieldParserInfo
    {
        public NetFieldParserInfo(NetFieldExportGroupInfo netFieldExportGroupInfo)
        {
            CoreRedirects = new CoreRedirects(netFieldExportGroupInfo);
        }

        public KeyList<string, NetFieldGroupInfo> NetFieldGroups { get; } = new();

        public Dictionary<Type, RepLayoutCmdType> PrimitiveTypeLayout { get; } = new();

        /// <summary>
        /// Mapping from ClassNetCache -> Type path name
        /// </summary>
        public Dictionary<string, NetRpcFieldGroupInfo> NetRpcStructureTypes { get; } = new();

        /// <summary>
        /// Player controllers require 1 extra byte to be read when creating actor
        /// </summary>
        public HashSet<string> PlayerControllers { get; } = new();

        public CompiledLinqCache LinqCache { get; } = new();
        public CoreRedirects CoreRedirects { get; }
    }
}