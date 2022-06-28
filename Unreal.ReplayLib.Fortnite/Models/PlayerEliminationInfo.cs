using Unreal.ReplayLib.Fortnite.Models.Enums;
using Unreal.ReplayLib.Models;

namespace Unreal.ReplayLib.Fortnite.Models;

public class PlayerEliminationInfo
{
    public FVector? Location { get; internal set; }
    public PlayerTypes PlayerType { get; internal set; }

    public string Id { get; internal set; }
    public bool IsBot => PlayerType is PlayerTypes.Bot or PlayerTypes.NamedBot;
}