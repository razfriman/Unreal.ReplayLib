namespace Unreal.ReplayLib.Models;

public class ReplayEvent
{
    public string Id { get; set; }
    public string Group { get; set; }
    public string Metadata { get; set; }
    public uint StartTime { get; set; }
    public uint EndTime { get; set; }
    public int Length { get; set; }
    public long Position { get; set; }
}