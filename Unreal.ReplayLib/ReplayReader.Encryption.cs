using System.IO;
using System.Security.Cryptography;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib
{
    public abstract partial class ReplayReader<T> where T : Replay, new()
    {
        protected virtual UnrealBinaryReader Decrypt(UnrealBinaryReader archive, int size)
        {
            if (!Replay.Info.Encrypted)
            {
                //Not the best way as it's 2 rents, but it works for now
                using var buffer = archive.GetMemoryBuffer(size);
                var decryptedReader = new UnrealBinaryReader(size)
                {
                    EngineNetworkVersion = Replay.Header.EngineNetworkVersion,
                    NetworkVersion = Replay.Header.NetworkVersion,
                    ReplayHeaderFlags = Replay.Header.Flags,
                    ReplayVersion = Replay.Info.FileVersion
                };
                buffer.Stream.CopyTo(decryptedReader.BaseStream);
                decryptedReader.BaseStream.Seek(0, SeekOrigin.Begin);
                return decryptedReader;
            }

            var key = Replay.Info.EncryptionKey;
            
            using var aes = Aes.Create();
            aes.Key = key;
            var encryptedBytes = archive.ReadBytes(size);
            var decryptedBytes = aes.DecryptEcb(encryptedBytes, PaddingMode.PKCS7);
            var decrypted = new UnrealBinaryReader(new MemoryStream(decryptedBytes))
            {
                EngineNetworkVersion = Replay.Header.EngineNetworkVersion,
                NetworkVersion = Replay.Header.NetworkVersion,
                ReplayHeaderFlags = Replay.Header.Flags,
                ReplayVersion = Replay.Info.FileVersion
            };

            return decrypted;
        }
    }
}