using System;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib
{
    public abstract partial class ReplayReader<T> where T : Replay, new()
    {
        protected virtual void ReceivedNextBunch(DataBunch bunch)
        {
            // We received the next bunch. Basically at this point:
            // -We know this is in order if reliable
            // -We dont know if this is partial or not
            // If its not a partial bunch, of it completes a partial bunch, we can call ReceivedSequencedBunch to actually handle it

            // Note this bunch's retirement.
            if (bunch.BReliable)
            {
                // Reliables should be ordered properly at this point
                //check(Bunch.ChSequence == Connection->InReliable[Bunch.ChIndex] + 1);
                InReliable = bunch.ChSequence;
            }

            // merge
            if (bunch.BPartial)
            {
                if (bunch.BPartialInitial)
                {
                    if (PartialBunch != null)
                    {
                        if (!PartialBunch.BPartialFinal)
                        {
                            if (PartialBunch.BReliable)
                            {
                                if (bunch.BReliable)
                                {
                                    Logger?.LogError(
                                        "Reliable initial partial trying to destroy reliable initial partial");
                                    return;
                                }

                                Logger?.LogError(
                                    "Unreliable initial partial trying to destroy unreliable initial partial");
                                return;
                            }
                            // Incomplete partial bunch. 
                        }

                        PartialBunch = null;
                    }

                    // InPartialBunch = new FInBunch(Bunch, false);
                    PartialBunch = new DataBunch(bunch);
                    var bitsLeft = bunch.Archive.GetBitsLeft();
                    if (!bunch.BHasPackageMapExports && bitsLeft > 0)
                    {
                        if (bitsLeft % 8 != 0)
                        {
                            Logger?.LogError(
                                $"Corrupt partial bunch. Initial partial bunches are expected to be byte-aligned. BitsLeft = {bitsLeft % 8}.");
                            return;
                        }

                        PartialBunch.Archive.AppendDataFromChecked(bunch.Archive.ReadBits(bitsLeft));
                    }
                    else
                    {
                        //_logger?.LogInformation("Received New partial bunch. It only contained NetGUIDs.");
                    }

                    return;
                }
                else
                {
                    // Merge in next partial bunch to InPartialBunch if:
                    // -We have a valid InPartialBunch
                    // -The current InPartialBunch wasn't already complete
                    // -ChSequence is next in partial sequence
                    // -Reliability flag matches
                    var bSequenceMatches = false;

                    if (PartialBunch != null)
                    {
                        var bReliableSequencesMatches = bunch.ChSequence == PartialBunch.ChSequence + 1;
                        var bUnreliableSequenceMatches =
                            bReliableSequencesMatches || bunch.ChSequence == PartialBunch.ChSequence;

                        // Unreliable partial bunches use the packet sequence, and since we can merge multiple bunches into a single packet,
                        // it's perfectly legal for the ChSequence to match in this case.
                        // Reliable partial bunches must be in consecutive order though
                        bSequenceMatches = PartialBunch.BReliable
                            ? bReliableSequencesMatches
                            : bUnreliableSequenceMatches;
                    }

                    // if (InPartialBunch && !InPartialBunch->bPartialFinal && bSequenceMatches && InPartialBunch->bReliable == Bunch.bReliable)
                    if (PartialBunch is { BPartialFinal: false } && bSequenceMatches &&
                        PartialBunch.BReliable == bunch.BReliable)
                    {
                        var bitsLeft = bunch.Archive.GetBitsLeft();
                        if (!bunch.BHasPackageMapExports && bitsLeft > 0)
                        {
                            PartialBunch.Archive.AppendDataFromChecked(bunch.Archive.ReadBits(bitsLeft));
                            //Dispose as we're done with it
                            bunch.Archive.Dispose();
                            // InPartialBunch->AppendDataFromChecked( Bunch.GetDataPosChecked(), Bunch.GetBitsLeft() );
                        }

                        // Only the final partial bunch should ever be non byte aligned. This is enforced during partial bunch creation
                        // This is to ensure fast copies/appending of partial bunches. The final partial bunch may be non byte aligned.
                        if (!bunch.BHasPackageMapExports && !bunch.BPartialFinal && bitsLeft % 8 != 0)
                        {
                            Logger?.LogError(
                                "Corrupt partial bunch. Non-final partial bunches are expected to be byte-aligned.");
                            return;
                        }

                        // Advance the sequence of the current partial bunch so we know what to expect next
                        PartialBunch.ChSequence = bunch.ChSequence;

                        if (bunch.BPartialFinal)
                        {
                            // Logger?.LogDebug("Completed Partial Bunch.");

                            if (bunch.BHasPackageMapExports)
                            {
                                Logger?.LogError(
                                    "Corrupt partial bunch. Final partial bunch has package map exports.");
                                return;
                            }

                            // HandleBunch = InPartialBunch;
                            PartialBunch.BPartialFinal = true;
                            PartialBunch.BClose = bunch.BClose;
                            PartialBunch.BDormant = bunch.BDormant;
                            PartialBunch.CloseReason = bunch.CloseReason;
                            PartialBunch.BIsReplicationPaused = bunch.BIsReplicationPaused;
                            PartialBunch.BHasMustBeMappedGuids = bunch.BHasMustBeMappedGuids;

                            ReceivedSequencedBunch(PartialBunch);
                            //Done
                            PartialBunch.Archive.Dispose();
                            return;
                        }

                        return;
                    }
                    else
                    {
                        // Merge problem - delete InPartialBunch. This is mainly so that in the unlikely chance that ChSequence wraps around, we wont merge two completely separate partial bunches.
                        // We shouldn't hit this path on 100% reliable connections
                        Logger?.LogError("Merge problem:  We shouldn't hit this path on 100% reliable connections");
                        return;
                    }
                }
                // bunch size check...
            }

            // something with opening channels...

            // Receive it in sequence.
            ReceivedSequencedBunch(bunch);
        }

        protected virtual bool ReceivedSequencedBunch(DataBunch bunch)
        {
            switch (bunch.ChType)
            {
                case ChannelType.Control:
                    ReceivedControlBunch(bunch);
                    break;
                default:
                    ReceivedActorBunch(bunch);
                    break;
            }

            if (bunch.BClose)
            {
                // We have fully received the bunch, so process it.
                //ChannelActors[bunch.ChIndex] = false;
                Channels[bunch.ChIndex] = null;
                OnChannelClosed(bunch.ChIndex);
                return true;
            }

            return false;
        }

        protected virtual void ReceivedControlBunch(DataBunch bunch)
        {
            // control channel
            while (!bunch.Archive.AtEnd())
            {
                var messageType = bunch.Archive.ReadByte();
            }
        }

        protected virtual void ReceivedActorBunch(DataBunch bunch)
        {
            if (bunch.BHasMustBeMappedGuids)
            {
                var numMustBeMappedGuids = bunch.Archive.ReadUInt16();
                for (var i = 0; i < numMustBeMappedGuids; i++)
                {
                    var guid = bunch.Archive.ReadPackedUInt32();
                }
            }

            ProcessBunch(bunch);
        }

        protected virtual void ProcessBunch(DataBunch bunch)
        {
            var channel = Channels[bunch.ChIndex];

            if (channel?.IgnoreChannel == true)
            {
                return;
            }

            var actor = Channels[bunch.ChIndex].Actor != null;

            if (!actor)
            {
                if (!bunch.BOpen)
                {
                    Logger?.LogError("New actor channel received non-open packet.");
                    return;
                }

                #region SerializeNewActor https: //github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Engine/Private/PackageMapClient.cpp#L257

                var inActor = new Actor
                {
                    // Initialize client if first time through.

                    // SerializeNewActor
                    // https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Engine/Private/PackageMapClient.cpp#L257
                    ActorNetGuid = InternalLoadObject(bunch.Archive, false)
                };

                if (bunch.Archive.AtEnd() && inActor.ActorNetGuid.IsDynamic())
                {
                    return;
                }

                if (inActor.ActorNetGuid.IsDynamic())
                {
                    inActor.Archetype = InternalLoadObject(bunch.Archive, false);

                    // if (Ar.IsSaving() || (Connection && (Connection->EngineNetworkProtocolVersion >= EEngineNetworkVersionHistory::HISTORY_NEW_ACTOR_OVERRIDE_LEVEL)))
                    if (bunch.Archive.EngineNetworkVersion >= EngineNetworkVersionHistory.HistoryNewActorOverrideLevel)
                    {
                        inActor.Level = InternalLoadObject(bunch.Archive, false);
                    }


                    inActor.Location = ConditionallySerializeQuantizedVector(new FVector(0, 0, 0), bunch);

                    // bSerializeRotation
                    if (bunch.Archive.ReadBit())
                    {
                        inActor.Rotation = bunch.Archive.ReadRotationShort();
                    }
                    else
                    {
                        inActor.Rotation = new FRotator(0, 0, 0);
                    }


                    inActor.Scale = ConditionallySerializeQuantizedVector(new FVector(1, 1, 1), bunch);
                    inActor.Velocity = ConditionallySerializeQuantizedVector(new FVector(0, 0, 0), bunch);
                }

                #endregion

                channel.Actor = inActor;

                OnChannelActorRead(channel.ChannelIndex, inActor);


                if (Channels[bunch.ChIndex].Actor.Archetype != null &&
                    GuidCache.TryGetNetGuidToPath(Channels[bunch.ChIndex].Actor.Archetype.Value,
                        out var pathName))
                {
                    if (NetFieldParser.IsPlayerController(pathName))
                    {
                        // netPlayerIndex
                        bunch.Archive.SkipBytes(1);
                    }
                }

                //ChannelNetGuids[bunch.ChIndex] = inActor.ActorNetGUID.Value;
            }

            // RepFlags.bNetOwner = true; // ActorConnection == Connection is always true??

            //RepFlags.bIgnoreRPCs = Bunch.bIgnoreRPCs;
            //RepFlags.bSkipRoleSwap = bSkipRoleSwap;

            //  Read chunks of actor content
            var innerArchiveError = false;

            while (!bunch.Archive.AtEnd() && !innerArchiveError)
            {
                var repObject = ReadContentBlockPayload(bunch, out var bObjectDeleted, out var bHasRepLayout,
                    out var payload);

                NetBitReader reader = null;

                if (payload > 0)
                {
                    bunch.Archive.SetTempEnd((int)payload, 3);
                    reader = bunch.Archive;
                }

                try
                {
                    if (bObjectDeleted)
                    {
                        continue;
                    }

                    if (bunch.Archive.IsError)
                    {
                        Logger?.LogError(
                            $"UActorChannel::ReceivedBunch: ReadContentBlockPayload FAILED. Bunch Info: {bunch}");
                        break;
                    }

                    if (repObject == 0 || reader == null || reader.AtEnd())
                    {
                        // Nothing else in this block, continue on (should have been a delete or create block)
                        continue;
                    }

                    //Channel's being ignored
                    if (Channels[bunch.ChIndex].IgnoreChannel == true)
                    {
                        continue;
                    }

                    ReceivedReplicatorBunch(bunch, reader, repObject, bHasRepLayout);
                }
                finally
                {
                    innerArchiveError = bunch.Archive.IsError;

                    if (payload > 0)
                    {
                        bunch.Archive.RestoreTemp(3);
                    }
                }
            }
            // PostReceivedBunch, not interesting?
        }

        protected virtual bool ReceivedReplicatorBunch(DataBunch bunch, NetBitReader archive, uint repObject,
            bool bHasRepLayout)
        {
            // outer is used to get path name
            // coreredirects.cpp ...
            var netFieldExportGroup = GuidCache.GetNetFieldExportGroup(repObject);
        
            //Mainly props. If needed, add them in
            if (netFieldExportGroup == null)
            {
                // Logger?.LogWarning($"Failed group. {bunch.ChIndex}");
                return true;
            }
        
            // Handle replayout properties
            if (bHasRepLayout)
            {
                // if ENABLE_PROPERTY_CHECKSUMS
                //var doChecksum = archive.ReadBit();
        
                if (!ReceiveProperties(archive, netFieldExportGroup, bunch.ChIndex, out var export))
                {
                    //Either failed to read properties or ignoring the channel
                    return false;
                }
            }
        
            if (archive.AtEnd())
            {
                return true;
            }
        
            var classNetCache = GuidCache.GetNetFieldExportGroupForClassNetCache(
                netFieldExportGroup.PathName);
            
            if (classNetCache == null)
            {
                return false;
            }
        
            while (ReadFieldHeaderAndPayload(archive, classNetCache, out var fieldCache, out var payload))
            {
                try
                {
                    NetBitReader reader = null;
        
                    if (payload.HasValue)
                    {
                        archive.SetTempEnd((int)payload, 5);
                        reader = archive;
                    }
        
                    if (fieldCache == null)
                    {
                        //Logger?.LogInformation($"ReceivedBunch: FieldCache == null Path: {classNetCache.PathName}");
                        continue;
                    }

                    if (fieldCache.Incompatible)
                    {
                        // We've already warned about this property once, so no need to continue to do so
                        Logger?.LogInformation($"ReceivedBunch: FieldCache->bIncompatible == true: {fieldCache.Name}");
                        continue;
                    }
        
                    if (reader == null || reader.IsError)
                    {
                        Logger?.LogInformation(
                            $"ReceivedBunch: reader == nullptr or IsError: {classNetCache.PathName}");
                        continue;
                    }
        
                    if (reader.AtEnd())
                    {
                        continue;
                    }

                    
                    //Find export group
                    var rpcGroupFound = NetFieldParser.TryGetNetFieldGroupRpc(classNetCache.PathName, fieldCache.Name,
                        out var netFieldInfo, out var willParse);
        
                    if (rpcGroupFound)
                    {
                        if (!willParse)
                        {
                            return true;
                        }
        
                        var isFunction = netFieldInfo.Attribute.IsFunction;
                        var pathName = netFieldInfo.Attribute.TypePathName;
                        var customSerialization = netFieldInfo.IsCustomStructure;
        
                        var exportGroup = GuidCache.GetNetFieldExportGroup(pathName);
        
                        if (isFunction)
                        {
                            if (exportGroup == null)
                            {
                                Logger?.LogError(
                                    $"Failed to find export group for function property {fieldCache.Name} {classNetCache.PathName}. BunchIndex: {BunchIndex}");
        
                                return false;
                            }
        
                            if (!ReceivedRpc(reader, exportGroup, bunch.ChIndex))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if (customSerialization)
                            {
                                if (!ReceiveCustomProperty(reader, classNetCache, fieldCache, bunch.ChIndex))
                                {
                                    Logger?.LogError(
                                        $"Failed to parse custom property {classNetCache.PathName} {fieldCache.Name}");
                                }
                            }
                            else if (exportGroup != null)
                            {
                                if (!ReceiveCustomDeltaProperty(reader, classNetCache, fieldCache.Handle,
                                    bunch.ChIndex))
                                {
                                    Logger?.LogError(
                                        $"Failed to find custom delta property {fieldCache.Name}. BunchIndex: {BunchIndex}");
        
                                    return false;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (payload.HasValue)
                    {
                        archive.RestoreTemp(5);
                    }
                }
            }
        
            return true;
        }
        
        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/bf95c2cbc703123e08ab54e3ceccdd47e48d224a/Engine/Source/Runtime/Engine/Private/DataReplication.cpp#L1158
        /// see https://github.com/EpicGames/UnrealEngine/blob/8776a8e357afff792806b997fbbd8e715111a271/Engine/Source/Runtime/Engine/Private/RepLayout.cpp#L5801
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        protected virtual bool ReceivedRpc(NetBitReader reader, NetFieldExportGroup netFieldExportGroup,
            uint channelIndex)
        {
            ReceiveProperties(reader, netFieldExportGroup, channelIndex, out var export);
        
            if (reader.IsError)
            {
                Logger?.LogError("ReceivedRPC: ReceivePropertiesForRPC - Reader.IsError() == true");
                return false;
            }
        
            if (!reader.AtEnd())
            {
                // Logger?.LogError("ReceivedRPC: ReceivePropertiesForRPC - Mismatch read.");
                // return false;
            }
        
            return true;
        }
        
        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/8776a8e357afff792806b997fbbd8e715111a271/Engine/Source/Runtime/Engine/Private/RepLayout.cpp#L3744
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        protected virtual bool ReceiveCustomDeltaProperty(NetBitReader reader,
            NetFieldExportGroup netFieldExportGroup,
            uint handle, uint channelIndex)
        {
            var bSupportsFastArrayDeltaStructSerialization = false;
        
            if (Replay.Header.EngineNetworkVersion >= EngineNetworkVersionHistory.HistoryFastArrayDeltaStruct)
            {
                // bSupportsFastArrayDeltaStructSerialization
                bSupportsFastArrayDeltaStructSerialization = reader.ReadBit();
            }
        
            //Need to figure out which properties require this
            //var staticArrayIndex = reader.ReadPackedUInt32();
        
            if (NetDeltaSerialize(reader, bSupportsFastArrayDeltaStructSerialization, netFieldExportGroup, handle,
                channelIndex))
            {
                // Successfully received it.
                return true;
            }
        
            return false;
        }
        
        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/8776a8e357afff792806b997fbbd8e715111a271/Engine/Source/Runtime/Engine/Classes/Engine/NetSerialization.h#L1064
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        protected virtual bool NetDeltaSerialize(NetBitReader reader, bool bSupportsFastArrayDeltaStructSerialization,
            NetFieldExportGroup netFieldExportGroup, uint handle, uint channelIndex)
        {
            if (!bSupportsFastArrayDeltaStructSerialization)
            {
                //No support for now
                return false;
            }
        
            var pathName = NetFieldParser.GetClassNetPropertyPathname(netFieldExportGroup.PathName,
                netFieldExportGroup.NetFieldExports[handle].Name, out var readChecksumBit);
        
            var propertyExportGroup = GuidCache.GetNetFieldExportGroup(pathName);
        
            var readProperties = propertyExportGroup != null &&
                                 NetFieldParser.WillReadType(propertyExportGroup.GroupId, out var _);
        
            if (!readProperties)
            {
                //Return true to prevent any warnings about failed readings
                return true;
            }
        
            var header = ReadDeltaHeader(reader);
        
            if (reader.IsError)
            {
                Logger?.LogError(
                    $"Failed to read DeltaSerialize header {netFieldExportGroup.PathName} {netFieldExportGroup.NetFieldExports[handle].Name}");
        
                return false;
            }
        
            for (var i = 0; i < header.NumDeleted; i++)
            {
                var elementIndex = reader.ReadInt32();
        
                if (propertyExportGroup != null)
                {
                    DeltaUpdate.Deleted = true;
                    DeltaUpdate.ChannelIndex = channelIndex;
                    DeltaUpdate.ElementIndex = elementIndex;
                    DeltaUpdate.ExportGroup = netFieldExportGroup;
                    DeltaUpdate.PropertyExport = propertyExportGroup;
                    DeltaUpdate.Handle = handle;
                    DeltaUpdate.Header = header;
                    OnNetDeltaRead(DeltaUpdate);
                }
            }
        
            for (var i = 0; i < header.NumChanged; i++)
            {
                var elementIndex = reader.ReadInt32();
        
                if (ReceiveProperties(reader, propertyExportGroup, channelIndex, out var export,
                    !readChecksumBit, true))
                {
                    DeltaUpdate.ChannelIndex = channelIndex;
                    DeltaUpdate.ElementIndex = elementIndex;
                    DeltaUpdate.Export = export;
                    DeltaUpdate.ExportGroup = netFieldExportGroup;
                    DeltaUpdate.PropertyExport = propertyExportGroup;
                    DeltaUpdate.Handle = handle;
                    DeltaUpdate.Header = header;
                    OnNetDeltaRead(DeltaUpdate);
                }
            }
        
            // if (reader.IsError || !reader.AtEnd())
            if (reader.IsError)
            {
                Logger?.LogError(
                    $"Failed to read DeltaSerialize {netFieldExportGroup.PathName} {netFieldExportGroup.NetFieldExports[handle].Name}");
        
                return false;
            }
        
            return true;
        }
        
        /// <summary>
        /// https://github.com/EpicGames/UnrealEngine/blob/bf95c2cbc703123e08ab54e3ceccdd47e48d224a/Engine/Source/Runtime/Engine/Classes/Engine/NetSerialization.h#L895
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private FFastArraySerializerHeader ReadDeltaHeader(NetBitReader reader)
        {
            var header = new FFastArraySerializerHeader
            {
                ArrayReplicationKey = reader.ReadInt32(),
                BaseReplicationKey = reader.ReadInt32(),
                NumDeleted = reader.ReadInt32(),
                NumChanged = reader.ReadInt32()
            };

            return header;
        }
        
        private bool ReceiveCustomProperty(NetBitReader reader, NetFieldExportGroup classNetCache,
            NetFieldExport fieldCache, uint channelIndex)
        {
            if (NetFieldParser.TryCreateRpcPropertyType(classNetCache.PathName, fieldCache.Name,
                out var customProperty))
            {
                try
                {
                    reader.SetTempEnd(reader.GetBitsLeft(), 2);
                    customProperty.Serialize(reader);
                    OnExportRead(channelIndex, customProperty as NetFieldExportGroupBase);
                    return true;
                }
                finally
                {
                    reader.RestoreTemp(2);
                }
            }
            else
            {
                return false;
            }
        }
        
        /// <summary>
        ///  https://github.com/EpicGames/UnrealEngine/blob/bf95c2cbc703123e08ab54e3ceccdd47e48d224a/Engine/Source/Runtime/Engine/Private/RepLayout.cpp#L2895
        ///  https://github.com/EpicGames/UnrealEngine/blob/bf95c2cbc703123e08ab54e3ceccdd47e48d224a/Engine/Source/Runtime/Engine/Private/RepLayout.cpp#L2971
        ///  https://github.com/EpicGames/UnrealEngine/blob/bf95c2cbc703123e08ab54e3ceccdd47e48d224a/Engine/Source/Runtime/Engine/Private/RepLayout.cpp#L3022
        /// </summary>
        /// <param name="archive"></param>
        protected virtual bool ReceiveProperties(NetBitReader archive, NetFieldExportGroup group,
            uint channelIndex,
            out NetFieldExportGroupBase outExport, bool readChecksumBit = true, bool isDeltaRead = false)
        {
            outExport = null;
        
            if (!isDeltaRead) //Makes sure delta reads don't cause the channel to be ignored
            {
                if (!NetFieldParser.WillReadType(group.GroupId, out var ignoreChannel))
                {
                    if (ignoreChannel)
                    {
                        Channels[channelIndex].IgnoreChannel = ignoreChannel;
                    }
        
                    return false;
                }
            }
        
            if (readChecksumBit)
            {
                var doChecksum = archive.ReadBit();
            }
        
            //Debug("types", $"\n{group.PathName}");
        
            var exportGroup = NetFieldParser.CreateType(group.GroupId);
        
            if (exportGroup is null or DebuggingExportGroup)
            {
                exportGroup = new DebuggingExportGroup
                {
                    ExportGroup = group
                };
            }
        
            outExport = exportGroup;
            outExport.ChannelActor = Channels[channelIndex].Actor;
        
        
            var hasData = false;
        
            while (true)
            {
                var handle = archive.ReadPackedUInt32();
        
                if (handle == 0)
                {
                    break;
                }
        
                handle--;
        
                if (group.NetFieldExports.Length <= handle)
                {
                    //Need to figure these out
                    //_logger.LogError($"NetFieldExport length ({group.NetFieldExports.Length}) < handle ({handle}) {group.PathName}");
        
                    return false;
                }
        
                var export = group.NetFieldExports[handle];
                var numBits = archive.ReadPackedUInt32();
        
                if (numBits == 0)
                {
                    continue;
                }
        
                if (export == null)
                {
                    archive.SkipBits((int)numBits);
        
                    continue;
                }
        
                if (export.Incompatible)
                {
                    archive.SkipBits((int)numBits);
        
                    continue;
                }
        
                hasData = true;
        
                try
                {
                    archive.SetTempEnd((int)numBits, 1);

                    NetFieldParser.ReadField(exportGroup, export, group, handle, archive);
        
                    if (archive.IsError)
                    {
                        Logger?.LogWarning(
                            $"Property {export.Name} caused error when reading (bits: {numBits}, group: {group.PathName})");
                        continue;
                    }
        
                    if (!archive.AtEnd())
                    {
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"NetFieldParser exception. Group: {group?.PathName} ExporT: {export.Name} Ex: {ex.Message} Stack: {ex.StackTrace}");
                }
                finally
                {
                    archive.RestoreTemp(1);
                }
            }
        
            //Delta structures are handled differently
            if (hasData && !isDeltaRead)
            {
                OnExportRead(channelIndex, exportGroup);
            }
        
            Channels[channelIndex].IgnoreChannel ??= false;
        
            return true;
        }
        
        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/bf95c2cbc703123e08ab54e3ceccdd47e48d224a/Engine/Source/Runtime/Engine/Private/DataChannel.cpp#L3579
        /// </summary>
        protected virtual bool ReadFieldHeaderAndPayload(NetBitReader bunch, NetFieldExportGroup group,
            out NetFieldExport outField, out uint? payload)
        {
            payload = null;
            if (bunch.AtEnd())
            {
                outField = null;
                return false; //We're done
            }
        
            // const int32 NetFieldExportHandle = Bunch.ReadInt(FMath::Max(NetFieldExportGroup->NetFieldExports.Num(), 2));
            var netFieldExportHandle = bunch.ReadSerializedInt(Math.Max((int)group.NetFieldExportsLength, 2));
        
            if (bunch.IsError)
            {
                outField = null;
                Logger?.LogError("ReadFieldHeaderAndPayload: Error reading NetFieldExportHandle.");
                return false;
            }
        
            if (netFieldExportHandle >= group.NetFieldExportsLength)
            {
                outField = null;
        
                Logger?.LogError("ReadFieldHeaderAndPayload: netFieldExportHandle > NetFieldExportsLength.");
        
                return false;
            }
        
            outField = group.NetFieldExports[(int)netFieldExportHandle];
        
            var numPayloadBits = bunch.ReadPackedUInt32();
            if (bunch.IsError)
            {
                outField = null;
                Logger?.LogError("ReadFieldHeaderAndPayload: Error reading numbits.");
                return false;
            }
        
            payload = numPayloadBits;
        
            if (bunch.IsError)
            {
                Logger?.LogError(
                    $"ReadFieldHeaderAndPayload: Error reading payload. Bunch: {BunchIndex}, OutField: {netFieldExportHandle}");
                return false;
            }
        
            // More to read
            return true;
        }
        
        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Engine/Private/DataChannel.cpp#L3391
        /// </summary>
        protected virtual uint ReadContentBlockPayload(DataBunch bunch, out bool bObjectDeleted,
            out bool bOutHasRepLayout, out uint payload)
        {
            payload = 0;
            var repObject = ReadContentBlockHeader(bunch, out bObjectDeleted, out bOutHasRepLayout);
        
            if (bObjectDeleted)
            {
                return repObject;
            }
        
        
            payload = bunch.Archive.ReadPackedUInt32();
            return repObject;
        }
        
        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/Engine/Private/DataChannel.cpp#L3175
        /// </summary>
        protected virtual uint ReadContentBlockHeader(DataBunch bunch, out bool bObjectDeleted,
            out bool bOutHasRepLayout)
        {
            //  bool& bObjectDeleted, bool& bOutHasRepLayout 
            //var bObjectDeleted = false;
            bObjectDeleted = false;
            bOutHasRepLayout = bunch.Archive.ReadBit();
            var bIsActor = bunch.Archive.ReadBit();
            if (bIsActor)
            {
                // If this is for the actor on the channel, we don't need to read anything else
                return Channels[bunch.ChIndex].Actor.Archetype?.Value ??
                       Channels[bunch.ChIndex].Actor.ActorNetGuid.Value;
            }
        
            // We need to handle a sub-object
            // Manually serialize the object so that we can get the NetGUID (in order to assign it if we spawn the object here)
        
            var netGuid = InternalLoadObject(bunch.Archive, false);
        
            var bStablyNamed = bunch.Archive.ReadBit();
            if (bStablyNamed)
            {
                // If this is a stably named sub-object, we shouldn't need to create it. Don't raise a bunch error though because this may happen while a level is streaming out.
                return netGuid.Value;
            }
        
            // Serialize the class in case we have to spawn it.
            var classNetGuid = InternalLoadObject(bunch.Archive, false);
        
            //Object deleted
            if (!classNetGuid.IsValid())
            {
                bObjectDeleted = true;
            }
        
            if (bunch.Archive.EngineNetworkVersion >= EngineNetworkVersionHistory.HistorySubobjectOuterChain)
            {
                var bActorIsOuter = bunch.Archive.AtEnd() || bunch.Archive.ReadBit();
                if (!bActorIsOuter)
                {
                    // outerobject
                    InternalLoadObject(bunch.Archive, false);
                }
            }
        
            return classNetGuid.Value;
        }
        
        private FVector ConditionallySerializeQuantizedVector(FVector defaultValue, DataBunch bunch)
        {
            var bWasSerialized = bunch.Archive.ReadBit();
            if (bWasSerialized)
            {
                bool bShouldQuantize;
                if (bunch.Archive.EngineNetworkVersion < EngineNetworkVersionHistory.HistoryOptionallyQuantizeSpawnInfo)
                {
                    bShouldQuantize = true;
                }
                else
                {
                    bShouldQuantize = bunch.Archive.ReadBit();
                }

                if (bShouldQuantize)
                {
                    return bunch.Archive.ReadPackedVector(10, 24);
                }
                else
                {
                    return new FVector(bunch.Archive.ReadSingle(), bunch.Archive.ReadSingle(),
                        bunch.Archive.ReadSingle());
                }
            }
            else
            {
                return defaultValue;
            }
        }
    }
}