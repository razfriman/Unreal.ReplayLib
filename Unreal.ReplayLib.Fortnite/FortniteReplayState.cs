using Unreal.ReplayLib.Fortnite.Models;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.Fortnite;

public class FortniteReplayState : ReplayState
{
    public FortniteReplayState() : base()
    {
       
    }

    public List<FVector> ChestPositions { get; set; } = new();
    public List<SafeZone> SafeZones { get; set; } = new();
    public DateTimeOffset Timecode { get; set; }
}