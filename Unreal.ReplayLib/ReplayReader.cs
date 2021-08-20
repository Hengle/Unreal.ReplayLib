using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.Exceptions;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;
using Unreal.ReplayLib.NetFields;

namespace Unreal.ReplayLib
{
    public abstract partial class ReplayReader<T> where T : Replay, new()
    {
        protected const int DefaultMaxChannelSize = 32767;
        protected const uint FileMagic = 0x1CA2E27F;
        protected const uint NetworkMagic = 0x2CF5A13D;
        protected const uint MetadataMagic = 0x3D06B24E;

        protected readonly ILogger Logger;
        protected readonly NetGuidCache GuidCache;
        protected readonly NetFieldExportGroupInfo NetFieldExportGroupInfo;
        protected readonly PlaybackPacket CurrentPacket = new();
        protected readonly DataBunch CurrentBunch = new();
        protected readonly NetBitReader PacketReader = new();
        protected readonly NetBitReader ExportReader = new();
        protected readonly NetDeltaUpdate DeltaUpdate = new();
        protected readonly UChannel[] Channels = new UChannel[DefaultMaxChannelSize];
        protected readonly NetFieldParser NetFieldParser;

        protected T Replay { get; set; }
        protected int BunchIndex;
        protected int InPacketId;
        protected DataBunch PartialBunch;
        protected int InReliable;
        protected bool IsReading;
        
        protected ReplayReader(ILogger logger, NetFieldExportGroupInfo netFieldExportGroupInfo)
        {
            Logger = logger;
            NetFieldExportGroupInfo = netFieldExportGroupInfo;
            NetFieldParser = new NetFieldParser(NetFieldExportGroupInfo);
            GuidCache = new NetGuidCache(NetFieldParser);
        }

        private void Reset()
        {
            Replay = new T();
            Replay.NetGuidToPathName ??= GuidCache.NetGuidToPathName;
        }

        public T ReadReplay(string fileName)
        {
            using var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ReadReplay(stream);
        }

        public T ReadReplay(Stream stream)
        {
            using var archive = new UnrealBinaryReader(stream);
            return ReadReplay(archive);
        }

        public T ReadReplay(UnrealBinaryReader archive)
        {
            if (IsReading)
            {
                throw new InvalidOperationException("Multithreaded reading currently isn't supported");
            }

            var sw = Stopwatch.StartNew();
            try
            {
                Reset();
                OnStartParse();
                IsReading = true;
                ReadReplayInfo(archive);
                ReadReplayChunks(archive);
                HydrateExportGroupFieldDict();
                Cleanup();
                Replay.ParseTime = sw.ElapsedMilliseconds;
                OnFinishParse();
                return Replay;
            }
            finally
            {
                IsReading = false;
            }
        }

        private void HydrateExportGroupFieldDict()
        {
            foreach (var exportGroup in GuidCache.PathToExportGroupDict.Values)
            {
                Replay.ExportGroupFieldDict.TryAdd(exportGroup.PathName, new HashSet<string>());
                if (Replay.ExportGroupFieldDict.TryGetValue(exportGroup.PathName, out var keyset))
                {
                    exportGroup.NetFieldExports.Where(x => x != null)
                        .ToList()
                        .ForEach(key => { keyset.Add(key.Name); });
                }
            }
        }

        protected void Cleanup()
        {
            InReliable = 0;
            BunchIndex = 0;
            Array.Clear(Channels);
            GuidCache.Clear();
        }

        protected virtual void ReadReplayChunks(UnrealBinaryReader archive)
        {
            while (!archive.AtEnd() && ShouldContinueParsing())
            {
                ReadReplayChunk(archive);
            }
        }

        protected void ReadReplayChunk(UnrealBinaryReader archive)
        {
            var chunkType = archive.ReadUInt32AsEnum<ReplayChunkType>();
            var chunkSize = archive.ReadInt32();
            var offset = archive.Position;

            switch (chunkType)
            {
                case ReplayChunkType.Checkpoint:
                    // ReadCheckpoint(archive);
                    archive.Seek(chunkSize, SeekOrigin.Current);
                    break;
                case ReplayChunkType.Event:
                    ReadEvent(archive);
                    break;
                case ReplayChunkType.ReplayData:
                    if (NetFieldExportGroupInfo.Types.Any())
                    {
                        ReadReplayData(archive, (uint)chunkSize);    
                    }
                    else
                    {
                        archive.Seek(chunkSize, SeekOrigin.Current);
                        
                    }
                    break;
                case ReplayChunkType.Header:
                    ReadReplayHeader(archive);
                    break;
                case ReplayChunkType.Unknown:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(chunkType), chunkType, "Unknown chunk type");
            }

            if (archive.Position != offset + chunkSize)
            {
                Logger?.LogError($"Chunk ({chunkType}) at offset {offset} not correctly read...");
                archive.Seek(offset + chunkSize);
            }
        }
        
        protected virtual NetFieldExportGroup ReadNetFieldExportGroupMap(FArchive archive)
        {
            var group = new NetFieldExportGroup()
            {
                PathName = archive.ReadFString(),
                PathNameIndex = archive.ReadPackedUInt32(),
                NetFieldExportsLength = archive.ReadPackedUInt32()
            };

            group.NetFieldExports = new NetFieldExport[group.NetFieldExportsLength];

            for (var i = 0; i < group.NetFieldExportsLength; i++)
            {
                var netFieldExport = ReadNetFieldExport(archive);

                if (netFieldExport != null)
                {
                    group.NetFieldExports[netFieldExport.Handle] = netFieldExport;
                }
            }

            return group;
        }

        protected void ReadEvent(UnrealBinaryReader archive)
        {
            var eventInfo = new EventInfo
            {
                Id = archive.ReadFString(),
                Group = archive.ReadFString(),
                Metadata = archive.ReadFString(),
                StartTime = archive.ReadUInt32(),
                EndTime = archive.ReadUInt32(),
                SizeInBytes = archive.ReadInt32()
            };
            Replay.Events.Add(eventInfo);
            using var decryptedReader = Decrypt(archive, eventInfo.SizeInBytes);
            OnReadEvent(decryptedReader, eventInfo);
        }


        protected void ReadReplayData(UnrealBinaryReader archive, uint chunkSize = 0)
        {
            var info = new ReplayDataInfo();
            if (archive.ReplayVersion >= ReplayVersionHistory.StreamChunkTimes)
            {
                info.Start = archive.ReadUInt32();
                info.End = archive.ReadUInt32();
                info.Length = archive.ReadUInt32();
            }
            else
            {
                info.Length = chunkSize;
            }

            var memorySizeInBytes = archive.ReplayVersion >= ReplayVersionHistory.Encryption
                ? archive.ReadInt32()
                : (int)info.Length;

            using var decryptedReader = Decrypt(archive, (int)info.Length);
            using var binaryArchive = Decompress(decryptedReader, memorySizeInBytes);

            while (!binaryArchive.AtEnd())
            {
                ReadDemoFrame(binaryArchive);
            }
        }

        protected void ReadReplayHeader(FArchive archive)
        {
            var magic = archive.ReadUInt32();

            if (magic != NetworkMagic)
            {
                Logger?.LogError(
                    $"Header.Magic != NETWORK_DEMO_MAGIC. Header.Magic: {magic}, NETWORK_DEMO_MAGIC: {NetworkMagic}");
                throw new InvalidReplayException(
                    $"Header.Magic != NETWORK_DEMO_MAGIC. Header.Magic: {magic}, NETWORK_DEMO_MAGIC: {NetworkMagic}");
            }

            var header = new ReplayHeader
            {
                NetworkVersion = archive.ReadUInt32AsEnum<NetworkVersionHistory>()
            };
            switch (header.NetworkVersion)
            {
                case >= NetworkVersionHistory.HistoryPlusOne:
                    Logger.LogWarning($"Encountered unknown NetworkVersionHistory: {(int)header.NetworkVersion}");
                    break;
                case <= NetworkVersionHistory.HistoryExtraVersion:
                    Logger?.LogError(
                        $"Header.Version < MIN_NETWORK_DEMO_VERSION. Header.Version: {header.NetworkVersion}, MIN_NETWORK_DEMO_VERSION: {NetworkVersionHistory.HistoryExtraVersion}");
                    throw new InvalidReplayException(
                        $"Header.Version < MIN_NETWORK_DEMO_VERSION. Header.Version: {header.NetworkVersion}, MIN_NETWORK_DEMO_VERSION: {NetworkVersionHistory.HistoryExtraVersion}");
            }

            header.NetworkChecksum = archive.ReadUInt32();
            header.EngineNetworkVersion = archive.ReadUInt32AsEnum<EngineNetworkVersionHistory>();

            if (header.EngineNetworkVersion >= EngineNetworkVersionHistory.HistoryEnginenetversionPlusOne)
            {
                Logger.LogWarning(
                    $"Encountered unknown EngineNetworkVersionHistory: {(int)header.EngineNetworkVersion}");
            }

            header.GameNetworkProtocolVersion = archive.ReadUInt32();

            if (header.NetworkVersion >= NetworkVersionHistory.HistoryHeaderGuid)
            {
                header.Guid = archive.ReadGuid();
            }

            if (header.NetworkVersion >= NetworkVersionHistory.HistorySaveFullEngineVersion)
            {
                header.Major = archive.ReadUInt16();
                header.Minor = archive.ReadUInt16();
                header.Patch = archive.ReadUInt16();
                header.Changelist = archive.ReadUInt32();
                header.Branch = archive.ReadFString();

                archive.NetworkReplayVersion = new NetworkReplayVersion
                {
                    Major = header.Major,
                    Minor = header.Minor,
                    Patch = header.Patch,
                    Changelist = header.Changelist,
                    Branch = header.Branch
                };
            }
            else
            {
                header.Changelist = archive.ReadUInt32();
            }

            if (header.NetworkVersion > NetworkVersionHistory.HistoryMultipleLevels)
            {
                header.LevelNamesAndTimes = archive.ReadTupleArray(archive.ReadFString, archive.ReadUInt32);
            }

            if (header.NetworkVersion >= NetworkVersionHistory.HistoryHeaderFlags)
            {
                header.Flags = archive.ReadUInt32AsEnum<ReplayHeaderFlags>();
                archive.ReplayHeaderFlags = header.Flags;
            }

            header.GameSpecificData = archive.ReadArray(archive.ReadFString);

            archive.EngineNetworkVersion = header.EngineNetworkVersion;
            archive.NetworkVersion = header.NetworkVersion;

            PacketReader.EngineNetworkVersion = header.EngineNetworkVersion;
            PacketReader.NetworkVersion = header.NetworkVersion;
            PacketReader.ReplayHeaderFlags = header.Flags;

            ExportReader.EngineNetworkVersion = header.EngineNetworkVersion;
            ExportReader.NetworkVersion = header.NetworkVersion;
            ExportReader.ReplayHeaderFlags = header.Flags;

            Replay.Header = header;
        }

        protected void ReadReplayInfo(FArchive archive)
        {
            var magicNumber = archive.ReadUInt32();

            if (magicNumber != FileMagic)
            {
                Logger?.LogError("Invalid replay file");
                throw new InvalidReplayException("Invalid replay file");
            }

            var fileVersion = archive.ReadUInt32AsEnum<ReplayVersionHistory>();
            archive.ReplayVersion = fileVersion;

            if (archive.ReplayVersion >= ReplayVersionHistory.NewVersion)
            {
                Logger.LogWarning($"Encountered unknown ReplayVersionHistory: {(int)archive.ReplayVersion}");
            }

            var info = new ReplayInfo
            {
                FileVersion = fileVersion,
                LengthInMs = archive.ReadUInt32(),
                NetworkVersion = archive.ReadUInt32(),
                Changelist = archive.ReadUInt32(),
                FriendlyName = archive.ReadFString(),
                IsLive = archive.ReadUInt32AsBoolean()
            };

            if (fileVersion >= ReplayVersionHistory.RecordedTimestamp)
            {
                info.Timestamp = archive.ReadDate();
            }

            if (fileVersion >= ReplayVersionHistory.Compression)
            {
                info.IsCompressed = archive.ReadUInt32AsBoolean();
            }

            if (fileVersion >= ReplayVersionHistory.Encryption)
            {
                info.Encrypted = archive.ReadUInt32AsBoolean();
                info.EncryptionKey = archive.ReadBytes(archive.ReadInt32());
            }

            if (!info.IsLive && info.Encrypted && info.EncryptionKey.Length == 0)
            {
                Logger?.LogError("ReadReplayInfo: Completed replay is marked encrypted but has no key!");
                throw new InvalidReplayException("Completed replay is marked encrypted but has no key!");
            }

            if (info.IsLive && info.Encrypted)
            {
                Logger?.LogError("ReadReplayInfo: Replay is marked encrypted and but not yet marked as completed!");
                throw new InvalidReplayException("Replay is marked encrypted and but not yet marked as completed!");
            }

            Replay.Info = info;
        }

        protected virtual void ReadExternalData(FArchive archive)
        {
            while (true)
            {
                var externalDataNumBits = archive.ReadPackedUInt32();
                if (externalDataNumBits == 0)
                {
                    return;
                }

                var netGuid = archive.ReadPackedUInt32();
                var data = GetExternalData();
                data.NetGuid = netGuid;
                data.TimeSeconds = CurrentPacket.TimeSeconds;
                var externalDataNumBytes = (int)(externalDataNumBits + 7) >> 3;
                data.Serialize(archive, externalDataNumBytes);
                OnExternalDataRead(data);
            }
        }
        
        protected virtual void OnReadHeader(ReplayHeader header)
        {
        }

        protected virtual void OnReadEvent(UnrealBinaryReader archive, EventInfo eventInfo)
        {
        }

        protected virtual void OnStreamingLevel(int currentLevelIndex, float timeSeconds, string levelName)
        {
        }

        protected virtual void OnExportRead(uint channel, NetFieldExportGroupBase exportGroup)
        {
        }

        protected virtual void OnNetDeltaRead(NetDeltaUpdate deltaUpdate)
        {
        }

        protected virtual void OnExternalDataRead(ExternalData data)
        {
        }

        protected virtual void OnChannelActorRead(uint channel, Actor actor)
        {
        }

        protected virtual void OnChannelClosed(uint channel)
        {
        }

        protected virtual void OnStartParse()
        {
        }
        
        protected virtual void OnFinishParse()
        {
        }

        protected virtual bool ShouldContinueParsing() => true;

        protected virtual ExternalData GetExternalData() => new();
    }
}