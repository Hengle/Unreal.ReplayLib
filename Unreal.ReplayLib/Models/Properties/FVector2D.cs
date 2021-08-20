using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models.Properties
{
    public class FVector2D : IProperty
    {
        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Core/Public/Math/Vector2D.h#L17
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public FVector2D(float x, float y)
        {
            X = x;
            Y = y;
        }

        public FVector2D()
        {
        }

        public float X { get; set; }
        public float Y { get; set; }

        public void Serialize(NetBitReader reader)
        {
            X = reader.SerializePropertyFloat();
            Y = reader.SerializePropertyFloat();
        }

        public override string ToString() => $"X: {X}, Y: {Y}";
    }
}