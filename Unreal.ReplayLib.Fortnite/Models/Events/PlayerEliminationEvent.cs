namespace Unreal.ReplayLib.Fortnite.Models.Events
{
    public class PlayerEliminationEvent : BaseEvent, IEquatable<PlayerEliminationEvent>
    {
        public PlayerEliminationInfo EliminatedInfo { get; internal set; }
        public PlayerEliminationInfo EliminatorInfo { get; internal set; }

        public string Eliminated => EliminatedInfo?.Id;
        public string Eliminator => EliminatorInfo?.Id;

        public EDeathCause GunType { get; internal set; } = EDeathCause.Unspecified;
        public uint Timestamp { get; internal set; }

        public bool Knocked { get; internal set; }
        public bool SelfElimination => Eliminated == Eliminator;

        public double Distance => EliminatorInfo?.Location?.DistanceTo(EliminatedInfo.Location) ?? -1;

        public bool Equals(PlayerEliminationEvent other)
        {
            if (other.Equals(null))
            {
                return false;
            }

            return Eliminated == other.Eliminated && Eliminator == other.Eliminator &&
                   GunType == other.GunType && Timestamp == other.Timestamp && Knocked == other.Knocked;
        }
    }
}