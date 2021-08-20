using System.IO;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;
using Unreal.ReplayLib.Models.Properties;

namespace Unreal.ReplayLib
{
    public abstract unsafe partial class ReplayReader<T> where T : Replay, new()
    {
        protected void ReadExportData(UnrealBinaryReader archive)
        {
            ReadNetFieldExports(archive);
            ReadNetExportGuids(archive);
        }

        protected void ReadNetExportGuids(UnrealBinaryReader archive)
        {
            var numGuids = archive.ReadPackedUInt32();
            // TODO bIgnoreReceivedExportGUIDs ?

            for (var i = 0; i < numGuids; i++)
            {
                var size = archive.ReadInt32();
                try
                {
                    ExportReader.SetBits(archive.BasePointer + archive.Position, size, size * 8);
                    archive.Seek(size, SeekOrigin.Current);
                    InternalLoadObject(ExportReader, true);
                }
                finally
                {
                    ExportReader.DisposeBits();
                }
            }
        }

        protected void ReadNetFieldExports(FArchive archive)
        {
            var numLayoutCmdExports = archive.ReadPackedUInt32();
            for (var i = 0; i < numLayoutCmdExports; i++)
            {
                var pathNameIndex = archive.ReadPackedUInt32();
                var isExported = archive.ReadPackedUInt32() == 1;
                var group = GuidCache.GetGroupByPathIndex(pathNameIndex);

                if (isExported)
                {
                    var pathName = archive.ReadFString();
                    var numExports = archive.ReadPackedUInt32();

                    if (group == null)
                    {
                        group = new NetFieldExportGroup
                        {
                            PathName = pathName,
                            PathNameIndex = pathNameIndex,
                            NetFieldExportsLength = numExports,
                            NetFieldExports = new NetFieldExport[numExports]
                        };
                        GuidCache.AddToExportGroupMap(pathName, group);
                    }
                    GuidCache.AddGroupByPathIndex(pathNameIndex, group);
                    GuidCache.AddGroupByPath(pathName, group);
                }

                var netField = ReadNetFieldExport(archive);
                
                if (group != null)
                {
                    group.NetFieldExports[netField.Handle] = netField;
                }
                else
                {
                    Logger?.LogError("ReceiveNetFieldExports: Unable to find NetFieldExportGroup for export.");
                }
            }
        }

        protected NetFieldExport ReadNetFieldExport(FArchive archive)
        {
            var isExported = archive.ReadBoolean();

            if (isExported)
            {
                var fieldExport = new NetFieldExport
                {
                    Handle = archive.ReadPackedUInt32(),
                    CompatibleChecksum = archive.ReadUInt32()
                };

                if (Replay.Header.EngineNetworkVersion < EngineNetworkVersionHistory.HistoryNetexportSerialization)
                {
                    fieldExport.Name = archive.ReadFString();
                    fieldExport.Type = archive.ReadFString();
                }
                else if (Replay.Header.EngineNetworkVersion <
                         EngineNetworkVersionHistory.HistoryNetexportSerializeFix)
                {
                    // FName
                    fieldExport.Name = archive.ReadFString();
                }
                else
                {
                    fieldExport.Name = ParseStaticName(archive);
                }

                return fieldExport;
            }

            return null;
        }

        protected NetworkGuid InternalLoadObject(FArchive archive, bool isExportingNetGuidBunch,
            int internalLoadObjectRecursionCount = 0)
        {
            if (internalLoadObjectRecursionCount > 16)
            {
                Logger?.LogWarning("InternalLoadObject: Hit recursion limit.");
                return new NetworkGuid();
            }

            var netGuid = new NetworkGuid
            {
                Value = archive.ReadPackedUInt32()
            };

            if (!netGuid.IsValid())
            {
                return netGuid;
            }

            var flags = ExportFlags.None;

            if (netGuid.IsDefault() || isExportingNetGuidBunch)
            {
                flags = archive.ReadByteAsEnum<ExportFlags>();
            }

            // outerguid
            if (flags.HasFlag(ExportFlags.BHasPath))
            {
                var outerGuid = InternalLoadObject(archive, true, internalLoadObjectRecursionCount + 1);
                var pathName = archive.ReadFString();

                if (flags.HasFlag(ExportFlags.BHasNetworkChecksum))
                {
                    archive.SkipBytes(4);
                }

                if (isExportingNetGuidBunch)
                {
                    GuidCache.AddPathByGuid(netGuid.Value, pathName);
                }

                return netGuid;
            }

            return netGuid;
        }

        protected string ParseStaticName(FArchive archive)
        {
            var isHardcoded = archive.ReadBoolean();
            if (isHardcoded)
            {
                var nameIndex = Replay.Header.EngineNetworkVersion < EngineNetworkVersionHistory.HistoryChannelNames
                    ? archive.ReadUInt32()
                    : archive.ReadPackedUInt32();

                return UnrealNameConstants.Names[nameIndex];
            }

            var inString = archive.ReadFString();
            archive.SkipBytes(4);
            return inString;
        }
    }
}