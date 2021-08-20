using System.IO;
using Unreal.ReplayLib.Compression;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib
{
    public abstract unsafe partial class ReplayReader<T> where T : Replay, new()
    {
        private UnrealBinaryReader Decompress(UnrealBinaryReader archive, int size) =>
            Replay.Info.IsCompressed
                ? DecompressOodle(archive)
                : CopyArchive(archive, size);

        private UnrealBinaryReader DecompressOodle(UnrealBinaryReader archive)
        {
            var decompressedSize = archive.ReadInt32();
            var compressedSize = archive.ReadInt32();
            using var compressedMemoryBuffer = archive.GetMemoryBuffer(compressedSize);

            var decompressed = new UnrealBinaryReader(decompressedSize)
            {
                EngineNetworkVersion = Replay.Header.EngineNetworkVersion,
                NetworkVersion = Replay.Header.NetworkVersion,
                ReplayHeaderFlags = Replay.Header.Flags,
                ReplayVersion = Replay.Info.FileVersion
            };

            Oodle.DecompressReplayData(compressedMemoryBuffer.PositionPointer, compressedSize, decompressed.BasePointer,
                decompressedSize);

            return decompressed;
        }

        private UnrealBinaryReader CopyArchive(UnrealBinaryReader archive, int size)
        {
            using var buffer = new MemoryStream(archive.ReadBytes(size));
            var uncompressed = new UnrealBinaryReader(size)
            {
                EngineNetworkVersion = Replay.Header.EngineNetworkVersion,
                NetworkVersion = Replay.Header.NetworkVersion,
                ReplayHeaderFlags = Replay.Header.Flags,
                ReplayVersion = Replay.Info.FileVersion
            };
            buffer.CopyTo(uncompressed.BaseStream);
            uncompressed.BaseStream.Seek(0, SeekOrigin.Begin);

            return uncompressed;
        }
    }
}