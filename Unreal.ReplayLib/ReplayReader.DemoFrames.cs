using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;
using Unreal.ReplayLib.Models.Properties;

namespace Unreal.ReplayLib
{
    public abstract unsafe partial class ReplayReader<T> where T : Replay, new()
    {
        protected void ReadDemoFrame(UnrealBinaryReader archive)
        {
            var currentLevelIndex = archive.NetworkVersion >= NetworkVersionHistory.HistoryMultipleLevels
                ? archive.ReadInt32()
                : 0;

            var timeSeconds = archive.ReadSingle();

            if (archive.NetworkVersion >= NetworkVersionHistory.HistoryLevelStreamingFixes)
            {
                ReadExportData(archive);
            }

            if (archive.HasLevelStreamingFixes())
            {
                var numStreamingLevels = archive.ReadPackedUInt32();
                for (var i = 0; i < numStreamingLevels; i++)
                {
                    var levelName = archive.ReadFString();
                    OnStreamingLevel(currentLevelIndex, timeSeconds, levelName);
                }
            }
            else
            {
                var numStreamingLevels = archive.ReadPackedUInt32();
                for (var i = 0; i < numStreamingLevels; i++)
                {
                    var packageName = archive.ReadFString();
                    var packageNameToLoad = archive.ReadFString();
                    var transform = new FTransform();
                    transform.Serialize(archive);
                    Logger.LogInformation($"Package Name: {packageName}");
                    Logger.LogInformation($"Package Name To Load: {packageNameToLoad}");
                }
            }

            if (archive.HasLevelStreamingFixes())
            {
                archive.SkipBytes(8);
            }

            ReadExternalData(archive);

            if (archive.HasGameSpecificFrameData())
            {
                var skipExternalOffset = archive.ReadUInt64();

                if (skipExternalOffset > 0)
                {
                    archive.SkipBytes((int)skipExternalOffset);
                }
            }

            var toContinue = true;
            while (toContinue)
            {
                uint seenLevelIndex = 0;

                if (archive.HasLevelStreamingFixes())
                {
                    seenLevelIndex = archive.ReadPackedUInt32();
                }

                var packet = ReadPacket(archive);
                packet.TimeSeconds = timeSeconds;
                packet.LevelIndex = currentLevelIndex;
                packet.SeenLevelIndex = seenLevelIndex;


                switch (packet.State)
                {
                    case PacketState.Success:
                        ReceivedRawPacket(packet, archive);
                        break;
                    case PacketState.End:
                    case PacketState.Error:
                        toContinue = false;
                        break;
                    default:
                        toContinue = false;
                        break;
                }
            }
        }
    }
}