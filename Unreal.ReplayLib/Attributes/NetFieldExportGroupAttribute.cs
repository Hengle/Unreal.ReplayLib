using System;

namespace Unreal.ReplayLib.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class NetFieldExportGroupAttribute : Attribute
    {
        public string Path { get; private set; }
        public string PlayerController { get; private set; }


        public NetFieldExportGroupAttribute(
            string path, 
            string playerController = null)
        {
            Path = path;
            PlayerController = playerController;
        }
    }
}