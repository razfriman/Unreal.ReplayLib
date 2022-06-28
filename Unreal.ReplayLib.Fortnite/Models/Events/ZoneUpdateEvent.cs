using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.Fortnite.Models.Events
{
    public class ZoneUpdateEvent : BaseEvent
    {
        public FVector Position { get; set; }
        public float Radius { get; set; }
    }
}