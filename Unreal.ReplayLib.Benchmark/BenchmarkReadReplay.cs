using BenchmarkDotNet.Attributes;
using Unreal.ReplayLib.Fortnite;

namespace Unreal.ReplayLib.Benchmark;

[MemoryDiagnoser]
[SimpleJob(1, 25, 25)]
public class BenchmarkReadReplay
{
    private readonly FortniteReplayReader _replayReader;

    private const string ReplayFile = "Replays/chapter2_season6_10.replay";

    public BenchmarkReadReplay()
    {
        _replayReader = new FortniteReplayReader(null);
    }

    [Benchmark]
    public void ParseReplay()
    {
        var replay = _replayReader.ReadReplay(ReplayFile);
    }
}