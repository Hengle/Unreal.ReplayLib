using System;

namespace Unreal.ReplayLib.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class PartialNetFieldExportGroup : Attribute
    {
        public string PartialPath { get; private set; }

        public PartialNetFieldExportGroup(string partialPath) => PartialPath = partialPath;
    }
}