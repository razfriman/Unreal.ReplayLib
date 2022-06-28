using System.Diagnostics;
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
    protected const uint FileMagic = 0x1CA2E27F;
    protected const uint NetworkMagic = 0x2CF5A13D;
    protected const uint MetadataMagic = 0x3D06B24E;

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
        var decrypted = Decrypt(archive, checkpoint.Length);
        Console.WriteLine("checkpoint");
    }
    
    private void ParseReplayData(ReplayData data, UnrealBinaryReader archive)
    {
        archive.Seek(data.Position);
        var decrypted = Decrypt(archive, data.CompressedLength);
        Console.WriteLine("data");
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