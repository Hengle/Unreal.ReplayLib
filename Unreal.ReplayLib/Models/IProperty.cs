using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models
{
    public interface IProperty
    {
        void Serialize(NetBitReader reader);
    }
}