using System.IO;
using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models.Properties
{
    /// <summary>
    /// https://github.com/EpicGames/UnrealEngine/blob/6c20d9831a968ad3cb156442bebb41a883e62152/Engine/Plugins/Runtime/GameplayAbilities/Source/GameplayAbilities/Private/GameplayEffectTypes.cpp#L311
    /// </summary>
    public class FGameplayEffectContextHandle : IProperty
    {
        public void Serialize(NetBitReader reader)
        {
            var validData = reader.ReadBit();

            if (!validData)
            {
                return;
            }

            //???
            reader.Seek(reader.GetBitsLeft(), SeekOrigin.Current);
        }
    }
}