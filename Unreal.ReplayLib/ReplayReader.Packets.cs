using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.Exceptions;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib
{
    public abstract unsafe partial class ReplayReader<T> where T : Replay, new()
    {
        protected virtual PlaybackPacket ReadPacket(FArchive archive)
        {
            var bufferSize = archive.ReadInt32();
            switch (bufferSize)
            {
                case 0:
                    CurrentPacket.State = PacketState.End;
                    return CurrentPacket;
                case > 2048:
                    Logger?.LogError("UDemoNetDriver::ReadPacket: OutBufferSize > 2048");
                    CurrentPacket.State = PacketState.Error;
                    return CurrentPacket;
                case < 0:
                    Logger?.LogError("UDemoNetDriver::ReadPacket: OutBufferSize < 0");

                    CurrentPacket.State = PacketState.Error;
                    return CurrentPacket;
            }

            CurrentPacket.DataLength = bufferSize;
            CurrentPacket.State = PacketState.Success;
            return CurrentPacket;
        }

        protected virtual void ReceivedRawPacket(PlaybackPacket packet, UnrealBinaryReader reader)
        {
            if (reader.HasLevelStreamingFixes() && packet.SeenLevelIndex == 0)
            {
                return;
            }

            if (packet.DataLength == 0)
            {
                Logger?.LogError("Received zero-size packet");
                return;
            }

            var ptr = reader.BasePointer + reader.Position;
            reader.Seek(packet.DataLength, SeekOrigin.Current);
            var lastByte = ptr[packet.DataLength - 1];

            if (lastByte != 0)
            {
                var bitSize = (packet.DataLength * 8) - 1;
                // Bit streaming, starts at the Least Significant Bit, and ends at the MSB.
                //while (!((lastByte & 0x80) >= 1))
                while (!((lastByte & 0x80) > 0))
                {
                    lastByte *= 2;
                    bitSize--;
                }

                PacketReader.SetBits(ptr, packet.DataLength, bitSize);

                try
                {
                    if (PacketReader.GetBitsLeft() > 0)
                    {
                        ReceivedPacket(PacketReader);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"failed ReceivedPacket");
                }
                finally
                {
                    PacketReader.DisposeBits();
                }
            }
            else
            {
                Logger?.LogError("Malformed packet: Received packet with 0's in last byte of packet");
                throw new MalformedPacketException("Malformed packet: Received packet with 0's in last byte of packet");
            }
        }

        protected virtual void ReceivedPacket(NetBitReader bitReader)
        {
            const int oldMaxActorChannels = 10240;
            InPacketId++;
            while (!bitReader.AtEnd())
            {
                if (bitReader.EngineNetworkVersion < EngineNetworkVersionHistory.HistoryAcksIncludedInHeader)
                {
                    var isAckDummy = bitReader.ReadBit();
                }

                var bunch = CurrentBunch;
                var bControl = bitReader.ReadBit();
                bunch.BOpen = bControl && bitReader.ReadBit();
                bunch.BClose = bControl && bitReader.ReadBit();

                if (bitReader.EngineNetworkVersion < EngineNetworkVersionHistory.HistoryChannelCloseReason)
                {
                    bunch.BDormant = bunch.BClose && bitReader.ReadBit();
                    bunch.CloseReason = bunch.BDormant ? ChannelCloseReason.Dormancy : ChannelCloseReason.Destroyed;
                }
                else
                {
                    bunch.CloseReason = bunch.BClose
                        ? (ChannelCloseReason)bitReader.ReadSerializedInt((int)ChannelCloseReason.Max)
                        : ChannelCloseReason.Destroyed;
                    bunch.BDormant = bunch.CloseReason == ChannelCloseReason.Dormancy;
                }

                bunch.BIsReplicationPaused = bitReader.ReadBit();
                bunch.BReliable = bitReader.ReadBit();

                bunch.ChIndex = bitReader.EngineNetworkVersion <
                                EngineNetworkVersionHistory.HistoryMaxActorChannelsCustomization
                    ? bitReader.ReadSerializedInt(oldMaxActorChannels)
                    : bitReader.ReadPackedUInt32();

                bunch.BHasPackageMapExports = bitReader.ReadBit();
                bunch.BHasMustBeMappedGuids = bitReader.ReadBit();
                bunch.BPartial = bitReader.ReadBit();

                if (bunch.BReliable)
                {
                    // We can derive the sequence for 100% reliable connections
                    bunch.ChSequence = InReliable + 1;
                }
                else if (bunch.BPartial)
                {
                    // If this is an unreliable partial bunch, we simply use packet sequence since we already have it
                    bunch.ChSequence = InPacketId;
                }
                else
                {
                    bunch.ChSequence = 0;
                }

                bunch.BPartialInitial = bunch.BPartial && bitReader.ReadBit();
                bunch.BPartialFinal = bunch.BPartial && bitReader.ReadBit();

                var chType = ChannelType.None;
                var chName = string.Empty;

                if (bitReader.EngineNetworkVersion < EngineNetworkVersionHistory.HistoryChannelNames)
                {
                    chType = bunch.BReliable || bunch.BOpen
                        ? (ChannelType)bitReader.ReadSerializedInt((int)ChannelType.Max)
                        : ChannelType.None;

                    chName = chType switch
                    {
                        ChannelType.Control => ChannelName.Control.ToString(),
                        ChannelType.Voice => ChannelName.Voice.ToString(),
                        ChannelType.Actor => ChannelName.Actor.ToString(),
                        _ => chName
                    };
                }
                else
                {
                    if (bunch.BReliable || bunch.BOpen)
                    {
                        if (bitReader.PeekBit())
                        {
                            chType = bitReader.ReadHardcodedName() switch
                            {
                                UnrealNames.Control => ChannelType.Control,
                                UnrealNames.Voice => ChannelType.Voice,
                                UnrealNames.Actor => ChannelType.Actor,
                                _ => chType
                            };
                        }
                        else //For backwards compatibility
                        {
                            chName = ParseStaticName(bitReader);

                            if (bitReader.IsError)
                            {
                                Logger?.LogError("Channel name serialization failed.");
                                return;
                            }

                            if (chName.Equals(ChannelName.Control.ToString()))
                            {
                                chType = ChannelType.Control;
                            }
                            else if (chName.Equals(ChannelName.Voice.ToString()))
                            {
                                chType = ChannelType.Voice;
                            }
                            else if (chName.Equals(ChannelName.Actor.ToString()))
                            {
                                chType = ChannelType.Actor;
                            }
                        }
                    }
                }

                bunch.ChType = chType;
                bunch.ChName = chName;

                var channel = Channels[bunch.ChIndex] != null;

                // https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Engine/Private/DemoNetDriver.cpp#L83
                var maxPacket = 1024 * 2;
                var bunchDataBits = bitReader.ReadSerializedInt(maxPacket * 8);

                //Too lazy to deal with this, but shouldn't affect performance much
                if (bunch.BPartial)
                {
                    bunch.Archive = bitReader.GetNetBitReader((int)bunchDataBits);
                }
                else
                {
                    bitReader.SetTempEnd((int)bunchDataBits);
                    bunch.Archive = bitReader;
                }

                bunch.Archive.EngineNetworkVersion = bitReader.EngineNetworkVersion;
                bunch.Archive.NetworkVersion = bitReader.NetworkVersion;
                bunch.Archive.ReplayHeaderFlags = bitReader.ReplayHeaderFlags;

                BunchIndex++;

                if (bunch.BHasPackageMapExports)
                {
                    ReceiveNetGuidBunch(bunch.Archive);
                }

                // Ignore if reliable packet has already been processed.
                if (bunch.BReliable && bunch.ChSequence <= InReliable)
                {
                    continue;
                }

                // If opening the channel with an unreliable packet, check that it is "bNetTemporary", otherwise discard it
                if (!channel && !bunch.BReliable)
                {
                    if (!(bunch.BOpen && (bunch.BClose || bunch.BPartial)))
                    {
                        continue;
                    }
                }

                // Create channel if necessary
                if (!channel)
                {
                    var newChannel = new UChannel
                    {
                        ChannelName = bunch.ChName,
                        ChannelType = bunch.ChType,
                        ChannelIndex = bunch.ChIndex
                    };

                    Channels[bunch.ChIndex] = newChannel;
                }

                try
                {
                    ReceivedNextBunch(bunch);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"failed ReceivedRawBunch, index: {BunchIndex}");
                }
                finally
                {
                    if (!bunch.BPartial)
                    {
                        bunch.Archive.RestoreTemp();
                    }
                }
            }

            if (!bitReader.AtEnd())
            {
                Logger?.LogWarning("Packet not fully read...");
            }
            // termination bit?
        }

        protected virtual void ReceiveNetGuidBunch(FBitArchive bitArchive)
        {
            var bHasRepLayoutExport = bitArchive.ReadBit();

            if (bHasRepLayoutExport)
            {
                ReceiveNetFieldExportsCompat(bitArchive);
                return;
            }

            var numGuidsInBunch = bitArchive.ReadInt32();
            const int maxGuidCount = 2048;
            if (numGuidsInBunch > maxGuidCount)
            {
                Logger?.LogError(
                    $"UPackageMapClient::ReceiveNetGUIDBunch: NumGUIDsInBunch > MAX_GUID_COUNT({numGuidsInBunch})");
                return;
            }

            var numGuidsRead = 0;
            while (numGuidsRead < numGuidsInBunch)
            {
                InternalLoadObject(bitArchive, true);
                numGuidsRead++;
            }
        }

        protected virtual void ReceiveNetFieldExportsCompat(FBitArchive bitArchive)
        {
            var numLayoutCmdExports = bitArchive.ReadUInt32();
            for (var i = 0; i < numLayoutCmdExports; i++)
            {
                var pathNameIndex = bitArchive.ReadPackedUInt32();
                var group = GuidCache.GetGroupByPathIndex(pathNameIndex);

                if (bitArchive.ReadBit())
                {
                    var pathName = bitArchive.ReadFString();
                    var numExports = bitArchive.ReadUInt32();
                    group = GuidCache.GetGroupByPathIndex(pathNameIndex);
                    if (group == null)
                    {
                        group = new NetFieldExportGroup
                        {
                            PathName = pathName,
                            PathNameIndex = pathNameIndex,
                            NetFieldExportsLength = numExports,
                            NetFieldExports = new NetFieldExport[numExports]
                        };
                        GuidCache.AddGroupByPath(pathName, group);
                    }

                    GuidCache.AddGroupByPathIndex(pathNameIndex, group);
                }

                var netField = ReadNetFieldExport(bitArchive);

                if (group.IsValidIndex(netField.Handle))
                {
                    //netField.Incompatible = group.NetFieldExports[(int)netField.Handle].Incompatible;
                    group.NetFieldExports[(int)netField.Handle] = netField;
                }
                else
                {
                    // ReceiveNetFieldExports: Invalid NetFieldExport Handle
                    // InBunch.SetError();
                }
            }
        }
    }
}