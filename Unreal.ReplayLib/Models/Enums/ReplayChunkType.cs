namespace Unreal.ReplayLib.Enums;

public enum ReplayChunkType: uint
{
    Header,
    ReplayData,
    Checkpoint,
    Event,
    Unknown = 0xFFFFFFFF
}