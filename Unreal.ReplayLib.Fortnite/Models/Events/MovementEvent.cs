using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.Fortnite.Models.Events
{
    public class MovementEvent : BaseEvent
    {
        public FVector Position { get; set; }
        public EFortMovementStyle MovementStyle { get; set; }
        public ushort DeltaGameTime { get; set; }
    }
}