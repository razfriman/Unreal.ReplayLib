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
                ReadReplayData(archive);
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
            var available = offset + chunkSize - archive.Position;
            Logger?.LogError("Chunk {ChunkType} at offset {Offset} not read correctly - {Available}", chunkType, offset,
                available);
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
            Length = archive.ReadInt32(),
            Position = archive.Position,
        };
        Replay.Checkpoints.Add(replayCheckpoint);
        archive.SkipBytes(replayCheckpoint.Length);
    }

    protected void ReadEvent(UnrealBinaryReader archive)
    {
        var replayEvent = new ReplayEvent
        {
            Id = archive.ReadFString(),
            Group = archive.ReadFString(),
            Metadata = archive.ReadFString(),
            StartTime = archive.ReadUInt32(),
            EndTime = archive.ReadUInt32(),
            Length = archive.ReadInt32(),
            Position = archive.Position
        };
        Replay.Events.Add(replayEvent);
        archive.SkipBytes(replayEvent.Length);
    }

    protected void ReadReplayData(UnrealBinaryReader archive)
    {
        var replayData = new ReplayData
        {
            Start = archive.ReadUInt32(),
            End = archive.ReadUInt32(),
            CompressedLength = archive.ReadInt32(),
            DecompressedLength = archive.ReadInt32(),
            Position = archive.Position
        };
        Replay.Data.Add(replayData);
        archive.SkipBytes(replayData.CompressedLength);
    }

    protected void ReadHeader(UnrealBinaryReader archive)
    {
        var magic = archive.ReadUInt32();

        if (magic != NetworkMagic)
        {
            Logger?.LogError(
                "Header.Magic != NETWORK_DEMO_MAGIC. Header.Magic: {Magic}, NETWORK_DEMO_MAGIC: {NetworkMagic}", magic,
                NetworkMagic);
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
                Logger?.LogWarning("Encountered unknown NetworkVersionHistory: {NetworkVersionHistory}",
                    (int)header.NetworkVersion);
                break;
            case <= NetworkVersionHistory.HistoryExtraVersion:
                Logger?.LogError(
                    "Header.Version < MIN_NETWORK_DEMO_VERSION. Header.Version: {NetworkVersion}, MIN_NETWORK_DEMO_VERSION: {HistoryExtraVersion}",
                    header.NetworkVersion, NetworkVersionHistory.HistoryExtraVersion);
                throw new ReplayException(
                    $"Header.Version < MIN_NETWORK_DEMO_VERSION. Header.Version: {header.NetworkVersion}, MIN_NETWORK_DEMO_VERSION: {NetworkVersionHistory.HistoryExtraVersion}");
        }

        header.NetworkChecksum = archive.ReadUInt32();
        header.EngineNetworkVersion = archive.ReadUInt32AsEnum<EngineNetworkVersionHistory>();

        if (header.EngineNetworkVersion >= EngineNetworkVersionHistory.HistoryEnginenetversionPlusOne)
        {
            Logger?.LogWarning(
                "Encountered unknown EngineNetworkVersionHistory: {EngineNetworkVersionHistory}",
                (int)header.EngineNetworkVersion);
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

        if (header.NetworkVersion >= NetworkVersionHistory.HistorySavePackageVersionUe)
        {
            // Engine package version on which the replay was recorded
            //FPackageFileVersion PackageVersionUE;
            // var ue4Version = archive.ReadInt32();
            // var ue5Version = archive.ReadUInt32AsEnum<UnrealEngineObjectUe5Version>();
            // int32 PackageVersionLicenseeUE;					// Licensee package version on which the replay was recorded
            // int packageVersionLicenseeUe = archive.ReadInt32();
        }

        if (header.NetworkVersion > NetworkVersionHistory.HistoryMultipleLevels)
        {
            header.LevelNamesAndTimes = archive.ReadTupleArray(archive.ReadFString, archive.ReadUInt32);
        }
        else if (header.NetworkVersion == NetworkVersionHistory.HistoryMultipleLevels)
        {
            var levelNames = archive.ReadArray(archive.ReadFString);
            header.LevelNamesAndTimes = levelNames.Select(x => (x, 0u)).ToArray();
        }
        else
        {
            var levelName = archive.ReadFString();
            header.LevelNamesAndTimes = new (string level, uint time)[]
            {
                (levelName, 0u)
            };
        }

        if (header.NetworkVersion >= NetworkVersionHistory.HistoryHeaderFlags)
        {
            header.Flags = archive.ReadUInt32AsEnum<ReplayHeaderFlags>();
            archive.ReplayHeaderFlags = header.Flags;
        }

        header.GameSpecificData = archive.ReadArray(archive.ReadFString);

        if (header.NetworkVersion >= NetworkVersionHistory.HistorySavePackageVersionUe)
        {
            header.MinRecordHz = archive.ReadSingle();
            header.MaxRecordHz = archive.ReadSingle();
            header.FrameLimitInMs = archive.ReadSingle();
            header.CheckpointLimitInMs = archive.ReadSingle();
            header.Platform = archive.ReadFString();
            header.BuildConfig = archive.ReadByteAsEnum<BuildConfiguration>();
            header.BuildTarget = archive.ReadByteAsEnum<BuildTargetType>();
        }

        Replay.Header = header;
        archive.EngineNetworkVersion = header.EngineNetworkVersion;
        archive.NetworkVersion = header.NetworkVersion;
    }
}