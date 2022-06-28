using Unreal.ReplayLib.Fortnite.Models.Events;

namespace Unreal.ReplayLib.Fortnite.Models;

public class CharacterSample
{
    public string EpicId { get; set; }
    public List<MovementEvent> MovementEvents { get; set; } = new();
}