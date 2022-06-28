using System.Security.Cryptography;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib;

public abstract partial class ReplayReader<TReplay, TState>
    where TReplay : Replay, new()
    where TState : ReplayState, new()
{
    protected virtual UnrealBinaryReader Decrypt(UnrealBinaryReader archive, int size)
    {
        if (!Replay.Info.Encrypted)
        {
            var buffer = archive.ReadBytes(size);
            var decryptedReader = new UnrealBinaryReader(buffer)
            {
                EngineNetworkVersion = Replay.Header.EngineNetworkVersion,
                NetworkVersion = Replay.Header.NetworkVersion,
                ReplayHeaderFlags = Replay.Header.Flags,
                ReplayVersion = Replay.Info.FileVersion
            };
            return decryptedReader;
        }

        using var aes = Aes.Create();
        aes.Key = Replay.Info.EncryptionKey;
        var encryptedBytes = archive.ReadBytes(size);
        var decryptedBytes = aes.DecryptEcb(encryptedBytes, PaddingMode.PKCS7);
        var decrypted = new UnrealBinaryReader(decryptedBytes)
        {
            EngineNetworkVersion = Replay.Header.EngineNetworkVersion,
            NetworkVersion = Replay.Header.NetworkVersion,
            ReplayHeaderFlags = Replay.Header.Flags,
            ReplayVersion = Replay.Info.FileVersion
        };
        return decrypted;
    }
}