namespace Unreal.ReplayLib.Enums;

public enum ReplayVersionHistory : uint
{
    Initial = 0,
    FixedSizeFriendlyName = 1,
    Compression = 2,
    RecordedTimestamp = 3,
    StreamChunkTimes = 4,
    FriendlyNameEncoding = 5,
    Encryption = 6,

    NewVersion,
    Latest = NewVersion - 1
}