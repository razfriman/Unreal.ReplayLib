using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.Enums;
using Unreal.ReplayLib.Exceptions;

namespace Unreal.ReplayLib;

public abstract partial class ReplayReader<T> where T : Replay, new()
{
    protected const uint FileMagic = 0x1CA2E27F;
    protected const uint NetworkMagic = 0x2CF5A13D;
    protected const uint MetadataMagic = 0x3D06B24E;

    protected readonly ILogger Logger;

    protected T Replay { get; set; }
    protected bool IsReading;

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
                // ReadCheckpoint(archive);
                archive.Seek(chunkSize, SeekOrigin.Current);
                break;
            case ReplayChunkType.Event:
                ReadEvent(archive);
                break;
            case ReplayChunkType.ReplayData:
                ReadReplayData(archive, (uint)chunkSize);
                archive.Seek(chunkSize, SeekOrigin.Current);
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

    private void ReadCheckpoint(UnrealBinaryReader archive)
    {
        var checkpointEvent = new ReplayCheckpointEvent
        {
            Id = archive.ReadFString(),
            Group = archive.ReadFString(),
            Metadata = archive.ReadFString(),
            StartTime = archive.ReadUInt32(),
            EndTime = archive.ReadUInt32(),
            SizeInBytes = archive.ReadInt32(),
            Position = archive.Position,
        };
        Replay.Events.Add(=);
    }

    protected void ReadEvent(UnrealBinaryReader archive)
    {
        var infoEvent = new ReplayInfoEvent()
        {
            Id = archive.ReadFString(),
            Group = archive.ReadFString(),
            Metadata = archive.ReadFString(),
            StartTime = archive.ReadUInt32(),
            EndTime = archive.ReadUInt32(),
            SizeInBytes = archive.ReadInt32(),
            Position = archive.Position,
        };
        Replay.Events.Add(infoEvent);
    }

    protected void ReadDataEvent(UnrealBinaryReader archive, uint chunkSize = 0)
    {
        var info = new ReplayDataEvent();
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
    }

    protected void ReadReplayHeader(UnrealBinaryReader archive)
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

        PacketReader.EngineNetworkVersion = header.EngineNetworkVersion;
        PacketReader.NetworkVersion = header.NetworkVersion;
        PacketReader.ReplayHeaderFlags = header.Flags;

        ExportReader.EngineNetworkVersion = header.EngineNetworkVersion;
        ExportReader.NetworkVersion = header.NetworkVersion;
        ExportReader.ReplayHeaderFlags = header.Flags;

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

        var info = new ReplayInfo
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
            info.Timestamp = reader.ReadDate();
        }

        if (fileVersion >= ReplayVersionHistory.Compression)
        {
            info.IsCompressed = reader.ReadUInt32AsBoolean();
        }

        if (fileVersion >= ReplayVersionHistory.Encryption)
        {
            info.Encrypted = reader.ReadUInt32AsBoolean();
            info.EncryptionKey = reader.ReadBytes(reader.ReadInt32());
        }

        if (!info.IsLive && info.Encrypted && info.EncryptionKey.Length == 0)
        {
            Logger?.LogError("ReadReplayInfo: Completed replay is marked encrypted but has no key!");
            throw new ReplayException("Completed replay is marked encrypted but has no key!");
        }

        if (info.IsLive && info.Encrypted)
        {
            Logger?.LogError("ReadReplayInfo: Replay is marked encrypted and but not yet marked as completed!");
            throw new ReplayException("Replay is marked encrypted and but not yet marked as completed!");
        }

        Replay.Info = info;
    }
}