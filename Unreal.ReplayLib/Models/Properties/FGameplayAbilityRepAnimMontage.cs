using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models.Properties
{
    public class FGameplayAbilityRepAnimMontage : IProperty
    {
        /** AnimMontage ref */
        public UObjectGuid AnimMontage { get; private set; }

        /** Play Rate */
        public float PlayRate { get; private set; }

        /** Montage position */
        public float Position { get; private set; }

        /** Montage current blend time */
        public float BlendTime { get; private set; }

        /** NextSectionID */
        public byte NextSectionId { get; private set; }

        /** flag indicating we should serialize the position or the current section id */
        public bool BRepPosition { get; private set; } = true;

        /** Bit set when montage has been stopped. */
        public bool IsStopped { get; private set; } = true;

        /** Bit flipped every time a new Montage is played. To trigger replication when the same montage is played again. */
        public bool ForcePlayBit { get; private set; }

        /** Stops montage position from replicating at all to save bandwidth */
        public bool SkipPositionCorrection { get; private set; }

        /** Stops PlayRate from replicating to save bandwidth. PlayRate will be assumed to be 1.f. */
        public bool BSkipPlayRate { get; private set; }

        public FPredictionKey PredictionKey { get; private set; }

        /** The current section Id used by the montage. Will only be valid if bRepPosition is false */
        public int SectionIdToPlay { get; private set; }

        public void Serialize(NetBitReader reader)
        {
            var repPosition = reader.ReadBoolean();

            if (repPosition)
            {
                BRepPosition = true;
                SectionIdToPlay = 0;
                SkipPositionCorrection = false;

                var packedPosition = reader.ReadPackedUInt32();

                Position = packedPosition / 100;
            }
            else
            {
                BRepPosition = false;

                SkipPositionCorrection = true;
                Position = 0;
                SectionIdToPlay = reader.ReadBitsToInt(7);
            }

            IsStopped = reader.ReadBit();
            ForcePlayBit = reader.ReadBit();
            SkipPositionCorrection = reader.ReadBit();
            BSkipPlayRate = reader.ReadBit();

            AnimMontage = new UObjectGuid {Value = reader.ReadPackedUInt32()};
            PlayRate = reader.SerializePropertyFloat();
            BlendTime = reader.SerializePropertyFloat();
            NextSectionId = reader.ReadByte();

            PredictionKey = new FPredictionKey();
            PredictionKey.Serialize(reader);
        }
    }
}