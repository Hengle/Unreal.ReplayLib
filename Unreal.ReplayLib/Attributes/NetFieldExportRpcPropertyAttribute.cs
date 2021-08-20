using System;
using Unreal.ReplayLib.Extensions;

namespace Unreal.ReplayLib.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class NetFieldExportRpcPropertyAttribute : Attribute
    {
        public string Name { get; private set; }
        public string TypePathName { get; private set; }
        public bool ReadChecksumBit { get; private set; }
        public bool IsFunction { get; private set; }
        public bool CustomStructure { get; private set; }

        public NetFieldExportRpcPropertyAttribute(string name, string typePathname, bool readChecksumBit = true,
            bool customStructure = false)
        {
            Name = name;
            TypePathName = typePathname.RemoveAllPathPrefixes();
            ReadChecksumBit = readChecksumBit;
            CustomStructure = customStructure;

            if (typePathname.Length > name.Length)
            {
                IsFunction = typePathname[^(name.Length + 1)] == ':';
            }
        }
    }
}