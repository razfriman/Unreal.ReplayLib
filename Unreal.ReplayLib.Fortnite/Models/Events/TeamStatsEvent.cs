namespace Unreal.ReplayLib.Fortnite.Models.Events
{
    public class TeamStatsEvent : BaseEvent
    {
        public uint Position { get; internal set; }
        public uint TotalPlayers { get; internal set; }
    }
}