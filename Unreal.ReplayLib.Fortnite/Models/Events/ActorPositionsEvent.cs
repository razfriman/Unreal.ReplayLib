using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.Fortnite.Models.Events
{
    public class ActorPositionsEvent : BaseEvent
    {
        public List<FVector> ChestPositions { get; set; } = new();
    }
}