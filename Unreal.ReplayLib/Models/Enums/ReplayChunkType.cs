namespace Unreal.ReplayLib.Models.Enums;

public enum ReplayChunkType: uint
{
    Header,
    ReplayData,
    Checkpoint,
    Event,
    Unknown = 0xFFFFFFFF
}