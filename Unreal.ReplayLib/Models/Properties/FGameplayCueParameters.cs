using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models.Properties
{
    public enum RepFlag
    {
        RepNormalizedMagnitude = 0,
        RepRawMagnitude,
        RepEffectContext,
        RepLocation,
        RepNormal,
        RepInstigator,
        RepEffectCauser,
        RepSourceObject,
        RepTargetAttachComponent,
        RepPhysMaterial,
        RepGeLevel,
        RepAbilityLevel,

        RepMax
    }

    public class FGameplayCueParameters : IProperty
    {
        public float NormalizedMagnitude { get; private set; }

        public float RawMagnitude { get; private set; }

        public FGameplayTagContainer AggregatedSourceTags { get; private set; } = new();

        /// <summary>
        /// The aggregated target tags taken from the effect spec
        /// </summary>
        public FGameplayTagContainer AggregatedTargetTags { get; private set; } = new();

        /// <summary>
        /// Location cue took place at
        /// </summary>
        public FVector? Location { get; private set; }

        /// <summary>
        /// Normal of impact that caused cue
        /// </summary>
        public FVector? Normal { get; private set; }

        /// <summary>
        /// Instigator actor, the actor that owns the ability system component
        /// </summary>
        public uint Instigator { get; private set; }

        /// <summary>
        /// The physical actor that actually did the damage, can be a weapon or projectile
        /// </summary>
        public uint EffectCauser { get; private set; }

        /// <summary>
        /// Object this effect was created from, can be an actor or static object. Useful to bind an effect to a gameplay object
        /// </summary>
        public uint SourceObject { get; private set; }

        /// <summary>
        ///  PhysMat of the hit, if there was a hit.
        /// </summary>
        public uint PhysicalMaterial { get; private set; }

        /// <summary>
        ///  If originating from a GameplayEffect, the level of that GameplayEffect
        /// </summary>
        public int GameplayEffectLevel { get; private set; }

        /// <summary>
        /// If originating from an ability, this will be the level of that ability
        /// </summary>
        public int AbilityLevel { get; private set; }

        /// <summary>
        /// Could be used to say "attach FX to this component always"
        /// </summary>
        public uint TargetAttachComponent { get; private set; }

        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/6c20d9831a968ad3cb156442bebb41a883e62152/Engine/Plugins/Runtime/GameplayAbilities/Source/GameplayAbilities/Private/GameplayEffectTypes.cpp#L789
        /// </summary>
        /// <param name="reader"></param>
        public void Serialize(NetBitReader reader)
        {
            const byte numLevelBits = 5; // need to bump this up to support 20 levels for AbilityLevel
            // const byte MAX_LEVEL = (1 << NUM_LEVEL_BITS) - 1;

            var repBits = reader.ReadBitsToInt((int) RepFlag.RepMax);

            // Tag containers serialize empty containers with 1 bit, so no need to serialize this in the RepBits field.
            AggregatedSourceTags.Serialize(reader);
            AggregatedTargetTags.Serialize(reader);

            if ((repBits & (1 << (int) RepFlag.RepNormalizedMagnitude)) > 0)
            {
                NormalizedMagnitude = reader.ReadSingle();
            }

            if ((repBits & (1 << (int) RepFlag.RepRawMagnitude)) > 0)
            {
                RawMagnitude = reader.ReadSingle();
            }

            if ((repBits & (1 << (int) RepFlag.RepEffectContext)) > 0)
            {
                // FGameplayEffectContextHandle
                if (reader.ReadBit())
                {
                }
            }

            if ((repBits & (1 << (int) RepFlag.RepLocation)) > 0)
            {
                Location = reader.SerializePropertyVector10();
            }

            if ((repBits & (1 << (int) RepFlag.RepNormal)) > 0)
            {
                Normal = reader.SerializePropertyVectorNormal();
            }

            if ((repBits & (1 << (int) RepFlag.RepInstigator)) > 0)
            {
                Instigator = reader.ReadPackedUInt32();
            }

            if ((repBits & (1 << (int) RepFlag.RepEffectCauser)) > 0)
            {
                EffectCauser = reader.ReadPackedUInt32();
            }

            if ((repBits & (1 << (int) RepFlag.RepSourceObject)) > 0)
            {
                SourceObject = reader.ReadPackedUInt32();
            }

            if ((repBits & (1 << (int) RepFlag.RepTargetAttachComponent)) > 0)
            {
                TargetAttachComponent = reader.ReadPackedUInt32();
            }

            if ((repBits & (1 << (int) RepFlag.RepPhysMaterial)) > 0)
            {
                PhysicalMaterial = reader.ReadPackedUInt32();
            }

            if ((repBits & (1 << (int) RepFlag.RepGeLevel)) > 0)
            {
                GameplayEffectLevel = reader.ReadBitsToInt(numLevelBits);
            }

            if ((repBits & (1 << (int) RepFlag.RepAbilityLevel)) > 0)
            {
                AbilityLevel = reader.ReadBitsToInt(numLevelBits);
            }

            if (!reader.AtEnd())
            {
            }
        }
    }
}