namespace Unreal.ReplayLib;

public class Replay
{
    public ReplayInfo Info { get; set; }
    public ReplayHeader Header { get; set; } = new();
    public List<ReplayInfoEvent> Events { get; } = new();
    public long ParseTime { get; set; }
}