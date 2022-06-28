namespace Unreal.ReplayLib.Fortnite.Models.Events
{
    public class StatsEvent : BaseEvent
    {
        public uint Eliminations { get; internal set; }
        public float Accuracy { get; internal set; }
        public uint Assists { get; internal set; }
        public uint WeaponDamage { get; internal set; }
        public uint OtherDamage { get; internal set; }
        public uint DamageToPlayers => WeaponDamage + OtherDamage;
        public uint Revives { get; internal set; }
        public uint DamageTaken { get; internal set; }
        public uint DamageToStructures { get; internal set; }
        public uint MaterialsGathered { get; internal set; }
        public uint MaterialsUsed { get; internal set; }
        public uint TotalTraveled { get; internal set; }
    }
}