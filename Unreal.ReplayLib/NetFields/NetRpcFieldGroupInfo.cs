using System.Collections.Generic;

namespace Unreal.ReplayLib.NetFields
{
    public sealed class NetRpcFieldGroupInfo
    {
        public Dictionary<string, NetRpcFieldInfo> PathNames { get; set; } = new();
    }
}