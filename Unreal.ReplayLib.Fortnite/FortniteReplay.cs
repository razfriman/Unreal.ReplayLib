using Unreal.ReplayLib.Fortnite.Models;
using Unreal.ReplayLib.Fortnite.Models.Events;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.Fortnite;

public class FortniteReplay : Replay
{
    public GameInformation GameInformation { get; set; } = new();
    public StatsEvent StatsEvent { get; internal set; }
    public TeamStatsEvent TeamStatsEvent { get; internal set; }
    public EncryptionKeyEvent PlayerStateEncryptionKeyEvent { get; set; }
    public List<PlayerEliminationEvent> Eliminations { get; set; } = new();
    public List<ZoneUpdateEvent> ZoneUpdateEvents { get; set; } = new();
    public List<CharacterSampleEvent> CharacterSampleEvents { get; set; } = new();
    public List<ActorPositionsEvent> ActorPositionEvents { get; set; } = new();
    public TimecodeEvent TimecodeEvent { get; set; }
}