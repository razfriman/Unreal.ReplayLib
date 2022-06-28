using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.Models;

public class ReplayHeader
{
    public NetworkVersionHistory NetworkVersion { get; set; }
    public uint NetworkChecksum { get; set; }
    public EngineNetworkVersionHistory EngineNetworkVersion { get; set; }
    public uint GameNetworkProtocolVersion { get; set; }
    public string Guid { get; set; }
    public ushort Major { get; set; }
    public ushort Minor { get; set; }
    public ushort Patch { get; set; }
    public uint Changelist { get; set; }
    public string Branch { get; set; }
    public (string level, uint time)[] LevelNamesAndTimes { get; set; }
    public ReplayHeaderFlags Flags { get; set; }
    public string[] GameSpecificData { get; set; }
    public float MinRecordHz { get; set; }
    public float MaxRecordHz { get; set; }
    public float FrameLimitInMs { get; set; }
    public float CheckpointLimitInMs { get; set; }
    public string Platform { get; set; }
    public BuildConfiguration BuildConfig { get; set; }
    public BuildTargetType BuildTarget { get; set; }
    public bool HasLevelStreamingFixes() => Flags.HasFlag(ReplayHeaderFlags.HasStreamingFixes);
}