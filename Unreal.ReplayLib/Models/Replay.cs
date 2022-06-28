namespace Unreal.ReplayLib.Models;

public class Replay
{
    public ReplayInfo Info { get; set; }
    public ReplayHeader Header { get; set; } = new();
    public List<ReplayEvent> Events { get; } = new();
    public List<ReplayData> Data { get; } = new();
    public List<ReplayCheckpoint> Checkpoints { get; } = new();
    public long ParseTime { get; set; }
}