using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.IO;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib;

public abstract partial class ReplayReader<TReplay, TState>
    where TReplay : Replay, new()
    where TState : ReplayState, new()
{
    protected const uint FileMagic = 0x1CA2E27F;
    protected const uint NetworkMagic = 0x2CF5A13D;

    protected readonly ILogger? Logger;

    protected TReplay Replay { get; set; }
    protected bool IsReading;
    protected TState State { get; set; } = new();

    protected ReplayReader(ILogger logger) => Logger = logger;

    private void Reset()
    {
        Replay = new TReplay();
        State = new TState();
    }

    public TReplay ReadReplay(string fileName)
    {
        var bytes = File.ReadAllBytes(fileName);
        using var ms = new MemoryStream(bytes);
        return ReadReplay(ms);
    }

    public TReplay ReadReplay(MemoryStream stream)
    {
        using var archive = new UnrealBinaryReader(stream);
        return ReadReplay(archive);
    }

    public TReplay ReadReplay(UnrealBinaryReader archive)
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
            ParseReplayChunks(archive);
            Replay.ParseTime = sw.ElapsedMilliseconds;
            return Replay;
        }
        finally
        {
            IsReading = false;
        }
    }

    private void ParseReplayChunks(UnrealBinaryReader archive)
    {
        ParseReplayEvents(archive);
        ParseReplayData(archive);
    }

    private void ParseReplayData(UnrealBinaryReader archive)
    {
        var time = 0u;
        if (State.UseCheckpoints && Replay.Checkpoints.Count > 0)
        {
            var checkpoint = Replay.Checkpoints[0];
            ParseReplayCheckpoint(checkpoint, archive);
            time = checkpoint.EndTime;
        }


        foreach (var replayCheckpoint in Replay.Checkpoints)
        {
            ParseReplayCheckpoint(replayCheckpoint, archive);
        }

        foreach (var replayData in Replay.Data)
        {
            ParseReplayData(replayData, archive);
        }
    }

    private void ParseReplayCheckpoint(ReplayCheckpoint checkpoint, UnrealBinaryReader archive)
    {
        archive.Seek(checkpoint.Position);
        using var decryptedReader = Decrypt(archive, checkpoint.Length);
        using var decompressedReader = Decompress(decryptedReader, checkpoint.Length);
        Console.WriteLine("checkpoint");
    }

    private void ParseReplayData(ReplayData data, UnrealBinaryReader archive)
    {
        archive.Seek(data.Position);
        using var decryptedReader = Decrypt(archive, data.CompressedLength);
        using var decompressedReader = Decompress(decryptedReader, data.DecompressedLength);
        while (!decompressedReader.AtEnd())
        {
            ParseReplayPacket(decompressedReader);
        }

        Console.WriteLine("data");
    }

    private void ParseReplayPacket(UnrealBinaryReader reader)
    {
        if (reader.NetworkVersion >= NetworkVersionHistory.HistoryMultipleLevels)
        {
            reader.SkipBytes(4); // current level index
        }

        var timeSeconds = reader.ReadSingle();

        if (reader.NetworkVersion >= NetworkVersionHistory.HistoryLevelStreamingFixes)
        {
            ReadNetFieldExports(reader);
            ReadNetExportGuids(reader);
        }

        if (reader.HasLevelStreamingFixes())
        {
            var numStreamingLevels = reader.ReadPackedUInt32();
            for (var i = 0; i < numStreamingLevels; i++)
            {
                var level = reader.ReadFString();
            }
        }

        if (reader.HasLevelStreamingFixes())
        {
            reader.SkipBytes(8);
        }

        ReadExternalData(reader);

        if (reader.HasGameSpecificFrameData())
        {
            var externalOffsetSize = reader.ReadUInt64();
            if (externalOffsetSize > 0)
            {
                reader.SkipBytes((int)externalOffsetSize);
            }
        }

        var done = false;

        while (!done)
        {
            var packet = ReadPacket(reader);
            packet.TimeSeconds = timeSeconds;
            reader.PushOffset(1, packet.Size);
            if (packet.State == 0)
            {
                ReceivedRawPacket(packet, reader);
            }
            else
            {
                reader.PopOffset(1);
                return;
            }

            reader.PopOffset(1);
        }
    }

    private void ReceivedRawPacket(object packet, UnrealBinaryReader reader)
    {
        // throw new NotImplementedException();
    }

    public class Packet
    {
        public uint StreamingFix { get; set; }
        public int Size { get; set; }
        public int State { get; set; }
        public float TimeSeconds { get; set; }
    }

    private Packet ReadPacket(UnrealBinaryReader reader)
    {
        var packet = new Packet();

        if (reader.HasLevelStreamingFixes())
        {
            packet.StreamingFix = reader.ReadPackedUInt32();
        }

        var bufferSize = reader.ReadInt32();

        packet.Size = bufferSize;

        if (bufferSize == 0)
        {
            packet.State = 1;
            return packet;
        }

        if (bufferSize > 2048 || bufferSize < 0)
        {
            packet.State = 2;
            return packet;
        }

        packet.State = 0;
        return packet;
    }

    private void ReadExternalData(UnrealBinaryReader reader)
    {
        while (true)
        {
            var externalDataNumBits = reader.ReadPackedUInt32();

            if (externalDataNumBits == 0)
            {
                return;
            }

            var netGuid = reader.ReadPackedUInt32();
            var externalDataNumBytes = (externalDataNumBits + 7) >> 3;
            var handle = reader.ReadByte();
            var something = reader.ReadByte();
            var isEncrypted = reader.ReadByte();
            var payload = reader.ReadBytes(externalDataNumBytes - 3);
            // globalData.externalData[netGuid] = externalData;
        }
    }

    private void ReadNetExportGuids(UnrealBinaryReader reader)
    {
        var numGuids = reader.ReadPackedUInt32();

        for (var i = 0; i < numGuids; i++)
        {
            var size = reader.ReadInt32();
            reader.PushOffset(2, size);
            // internalLoadObject(replay, true, globalData);
            reader.PopOffset(2);
        }
    }

    private void ReadNetFieldExports(UnrealBinaryReader reader)
    {
        var numLayoutCmdExports = reader.ReadPackedUInt32();

        for (var i = 0; i < numLayoutCmdExports; i++)
        {
            var pathNameIndex = reader.ReadPackedUInt32();
            var isExported = reader.ReadPackedUInt32() == 1;
            // NetFieldExportGroup group;

            if (isExported)
            {
                var pathname = reader.ReadFString();
                var numExports = reader.ReadPackedUInt32();

                // group = globalData.netGuidCache.NetFieldExportGroupMap[pathname];
                //
                // if (!group)
                // {
                //     group = new NetFieldExportGroup();
                //     group.pathName = pathname;
                //     group.pathNameIndex = pathNameIndex;
                //     group.netFieldExportsLength = numExports;
                //     group.netFieldExports =  []
                //     ;
                //
                //     globalData.netGuidCache.addToExportGroupMap(pathname, group, globalData);
                // }
                // else if (!group.netFieldExportsLength)
                // {
                //     group.netFieldExportsLength = numExports;
                //     group.pathNameIndex = pathNameIndex;
                //     globalData.netGuidCache.NetFieldExportGroupIndexToGroup[pathNameIndex] = pathname;
                // }
            }
            else
            {
                // group = globalData.netGuidCache.GetNetFieldExportGroupFromIndex(pathNameIndex);
            }

            //
            var netField = ReadNetFieldExport(reader);
            //
            // if (netField == null || group == null)
            // {
            //     continue;
            // }
            //
            // var netFieldExportGroup = globalData.netFieldParser.getNetFieldExport(group.pathName);
            //
            // if (netFieldExportGroup == null)
            // {
            //     if (group.parseUnknownHandles || group.pathName == = 'NetworkGameplayTagNodeIndex')
            //     {
            //         group.netFieldExports[netField.handle] = netField;
            //
            //         continue;
            //     }
            //
            //     addToUnreadGroups(group, netField, globalData);
            //     continue;
            // }
            //
            // const netFieldExport  = netFieldExportGroup.properties[netField.name];
            //
            // if (!netFieldExport)
            // {
            //     if (group.parseUnknownHandles || group.pathName == "NetworkGameplayTagNodeIndex")
            //     {
            //         group.netFieldExports[netField.handle] = netField;
            //
            //         continue;
            //     }
            //     addToUnreadGroups(group, netField, globalData);
            //     continue;
            // }
            //
            // group.netFieldExports[netField.handle] =  {
            //     ...netFieldExport,
            //     ...netField,
            // }
            // ;
        }
    }

    public class NetFieldExport
    {
        public string Name { get; set; }
        public string OrigType { get; set; }
        public uint Handle { get; set; }
        public uint CompatibleChecksum { get; set; }
    }

    private NetFieldExport ReadNetFieldExport(UnrealBinaryReader reader)
    {
        var isExported = reader.ReadByte();

        if (isExported > 0)
        {
            var fieldExport = new NetFieldExport
            {
                Handle = reader.ReadPackedUInt32(),
                CompatibleChecksum = reader.ReadUInt32(),
            };

            if (reader.EngineNetworkVersion < EngineNetworkVersionHistory.HistoryNetexportSerialization)
            {
                fieldExport.Name = reader.ReadFString();
                fieldExport.OrigType = reader.ReadFString();
            }
            else if (reader.EngineNetworkVersion < EngineNetworkVersionHistory.HistoryNetexportSerializeFix)
            {
                fieldExport.Name = reader.ReadFString();
            }
            else
            {
                fieldExport.Name = reader.ReadFName();
            }

            return fieldExport;
        }

        return null;
    }

    private void ParseReplayEvents(UnrealBinaryReader archive)
    {
        var replayEvents = Replay
            .Events
            .OrderBy(x => x.StartTime)
            .ToList();

        foreach (var replayEvent in replayEvents)
        {
            try
            {
                OnParseReplayEvent(replayEvent, archive);
            }
            catch (Exception e)
            {
                Logger?.LogError(e, "Error while handling event chunk");
            }
        }
    }

    public virtual void OnParseReplayEvent(ReplayEvent replayEvent, UnrealBinaryReader archive)
    {
    }
}