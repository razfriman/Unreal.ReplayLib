using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.Exceptions;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib;

public abstract partial class ReplayReader<TReplay, TState>
    where TReplay : Replay, new()
    where TState : ReplayState, new()
{
    protected void ReadReplayInfo(UnrealBinaryReader reader)
    {
        var magicNumber = reader.ReadUInt32();

        if (magicNumber != FileMagic)
        {
            Logger?.LogError("Invalid replay file");
            throw new ReplayException("Invalid replay file");
        }

        var fileVersion = reader.ReadUInt32AsEnum<ReplayVersionHistory>();
        reader.ReplayVersion = fileVersion;

        if (reader.ReplayVersion >= ReplayVersionHistory.NewVersion)
        {
            Logger?.LogWarning("Encountered unknown ReplayVersionHistory: {ReplayVersionHistory}", (int)reader.ReplayVersion);
        }

        var replayInfo = new ReplayInfo
        {
            FileVersion = fileVersion,
            LengthInMs = reader.ReadUInt32(),
            NetworkVersion = reader.ReadUInt32(),
            Changelist = reader.ReadUInt32(),
            FriendlyName = reader.ReadFString(),
            IsLive = reader.ReadUInt32AsBoolean()
        };

        if (fileVersion >= ReplayVersionHistory.RecordedTimestamp)
        {
            replayInfo.Timestamp = reader.ReadDate();
        }

        if (fileVersion >= ReplayVersionHistory.Compression)
        {
            replayInfo.IsCompressed = reader.ReadUInt32AsBoolean();
        }

        if (fileVersion >= ReplayVersionHistory.Encryption)
        {
            replayInfo.Encrypted = reader.ReadUInt32AsBoolean();
            replayInfo.EncryptionKey = reader.ReadBytes(reader.ReadInt32());
        }

        if (!replayInfo.IsLive && replayInfo.Encrypted && replayInfo.EncryptionKey.Length == 0)
        {
            Logger?.LogError("ReadReplayInfo: Completed replay is marked encrypted but has no key!");
            throw new ReplayException("Completed replay is marked encrypted but has no key!");
        }

        if (replayInfo.IsLive && replayInfo.Encrypted)
        {
            Logger?.LogError("ReadReplayInfo: Replay is marked encrypted and but not yet marked as completed!");
            throw new ReplayException("Replay is marked encrypted and but not yet marked as completed!");
        }

        Replay.Info = replayInfo;
    }
}