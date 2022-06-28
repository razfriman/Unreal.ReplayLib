namespace Unreal.ReplayLib.Fortnite.Models.Events
{
    public class CharacterSampleEvent : BaseEvent
    {
        public List<CharacterSample> Samples { get; set; } = new();
    }
}