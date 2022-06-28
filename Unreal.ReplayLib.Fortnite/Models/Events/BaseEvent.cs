using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.Fortnite.Models.Events
{
    public abstract class BaseEvent
    {
        public ReplayEvent ReplayEvent { get; internal set; }
    }
}