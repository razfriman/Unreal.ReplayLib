namespace Unreal.ReplayLib.Models.Enums;

public class NetworkReplayVersion
{
    public ushort Major { get; set; }
    public ushort Minor { get; set; }
    public ushort Patch { get; set; }
    public uint Changelist { get; set; }
    public string Branch { get; set; }
} 