using System;

namespace Unreal.ReplayLib.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class RedirectPathAttribute : Attribute
    {
        public string Path { get; private set; }

        public RedirectPathAttribute(string path) => Path = path;
    }
}