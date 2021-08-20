namespace Unreal.ReplayLib.Models.Enums
{
    /// <summary>
    /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Engine/Classes/Engine/DemoNetDriver.h#L930
    /// </summary>
    public enum CheckpointSaveState
    {
        ECheckpointSaveStateIdle,
        ECheckpointSaveStateProcessCheckpointActors,
        ECheckpointSaveStateSerializeDeletedStartupActors,
        ECheckpointSaveStateSerializeGuidCache,
        ECheckpointSaveStateSerializeNetFieldExportGroupMap,
        ECheckpointSaveStateSerializeDemoFrameFromQueuedDemoPackets,
        ECheckpointSaveStateFinalize
    }
}