namespace Unreal.ReplayLib;

public class ReplayInfoEvent
{
    public string Id { get; set; }
    public string Group { get; set; }
    public string Metadata { get; set; }
    public uint StartTime { get; set; }
    public uint EndTime { get; set; }
    public int SizeInBytes { get; set; }
    public long Position { get; set; }
}