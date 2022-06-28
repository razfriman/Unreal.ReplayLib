using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.Exceptions;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib;

public abstract partial class ReplayReader<T> where T : Replay, new()
{
    protected const uint FileMagic = 0x1CA2E27F;
    protected const uint NetworkMagic = 0x2CF5A13D;
    protected const uint MetadataMagic = 0x3D06B24E;

    protected readonly ILogger Logger;

    protected T Replay { get; set; }
    protected bool IsReading;
    protected ReplayState State { get; } = new();

    protected ReplayReader(ILogger logger) => Logger = logger;

    private void Reset() => Replay = new T();

    public T ReadReplay(string fileName)
    {
        var bytes = File.ReadAllBytes(fileName);
        using var ms = new MemoryStream(bytes);
        return ReadReplay(ms);
    }

    public T ReadReplay(MemoryStream stream)
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
            IsReading = true;
            ReadReplayInfo(archive);
            ReadReplayChunks(archive);
            Cleanup();
            Replay.ParseTime = sw.ElapsedMilliseconds;
            return Replay;
        }
        finally
        {
            IsReading = false;
        }
    }

    protected void Cleanup()
    {
    }

    protected virtual void ReadReplayChunks(UnrealBinaryReader archive)
    {
        while (!archive.AtEnd())
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
                ReadCheckpoint(archive);
                break;
            case ReplayChunkType.Event:
                ReadEvent(archive);
                break;
            case ReplayChunkType.ReplayData:
                ReadReplayData(archive, (uint)chunkSize);
                break;
            case ReplayChunkType.Header:
                ReadHeader(archive);
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

    private void ReadCheckpoint(UnrealBinaryReader archive)
    {
        var replayCheckpoint = new ReplayCheckpoint
        {
            Id = archive.ReadFString(),
            Group = archive.ReadFString(),
            Metadata = archive.ReadFString(),
            StartTime = archive.ReadUInt32(),
            EndTime = archive.ReadUInt32(),
            SizeInBytes = archive.ReadInt32(),
            Position = archive.Position,
        };
        Replay.Checkpoints.Add(replayCheckpoint);
    }

    protected void ReadEvent(UnrealBinaryReader archive)
    {
        var replayEvent = new ReplayEvent()
        {
            Id = archive.ReadFString(),
            Group = archive.ReadFString(),
            Metadata = archive.ReadFString(),
            StartTime = archive.ReadUInt32(),
            EndTime = archive.ReadUInt32(),
            SizeInBytes = archive.ReadInt32(),
            Position = archive.Position,
        };
        Replay.Events.Add(replayEvent);
    }

    protected void ReadReplayData(UnrealBinaryReader archive, uint chunkSize = 0)
    {
        var replayData = new ReplayData();
        if (archive.ReplayVersion >= ReplayVersionHistory.StreamChunkTimes)
        {
            replayData.Start = archive.ReadUInt32();
            replayData.End = archive.ReadUInt32();
            replayData.Length = archive.ReadUInt32();
        }
        else
        {
            replayData.Length = chunkSize;
        }

        var memorySizeInBytes = archive.ReplayVersion >= ReplayVersionHistory.Encryption
            ? archive.ReadInt32()
            : (int)replayData.Length;
        replayData.Size = memorySizeInBytes;

        Replay.Data.Add(replayData);
    }

    protected void ReadHeader(UnrealBinaryReader archive)
    {
        var magic = archive.ReadUInt32();

        if (magic != NetworkMagic)
        {
            Logger?.LogError(
                $"Header.Magic != NETWORK_DEMO_MAGIC. Header.Magic: {magic}, NETWORK_DEMO_MAGIC: {NetworkMagic}");
            throw new ReplayException(
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
                throw new ReplayException(
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

        Replay.Header = header;
    }

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
            Logger.LogWarning($"Encountered unknown ReplayVersionHistory: {(int)reader.ReplayVersion}");
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