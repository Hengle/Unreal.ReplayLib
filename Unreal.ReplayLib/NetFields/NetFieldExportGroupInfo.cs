using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.NetFields
{
    public class NetFieldExportGroupInfo
    {
        public ImmutableHashSet<Type> Types { get; }

        public static NetFieldExportGroupInfo None() => new(ImmutableHashSet<Type>.Empty);

        public static NetFieldExportGroupInfo FromTypes(ICollection<Type> types)
        {
            var baseTypes = typeof(NetFieldExport).Assembly.GetTypes().ToImmutableHashSet();
            return new NetFieldExportGroupInfo(baseTypes.Union(types));
        }

        public static NetFieldExportGroupInfo FromAssembly(Assembly assembly)
        {
            var baseTypes = typeof(NetFieldExport).Assembly.GetTypes().ToImmutableHashSet();
            return new NetFieldExportGroupInfo(baseTypes.Union(assembly.GetTypes()));
        }

        public NetFieldExportGroupInfo(ImmutableHashSet<Type> types) => Types = types;

        public override int GetHashCode() => HashCode.Combine(Types);
    }
}