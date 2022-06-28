namespace Unreal.ReplayLib.Models.Enums;

[Flags]
public enum ReplayHeaderFlags
{
    None = 0,
    ClientRecorded = 1 << 0,
    HasStreamingFixes = 1 << 1,
    DeltaCheckpoints = 1 << 2,
    GameSpecificFrameData = 1 << 3,
    ReplayConnection = 1 << 4 //TODO
}