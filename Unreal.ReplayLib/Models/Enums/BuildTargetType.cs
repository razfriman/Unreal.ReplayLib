namespace Unreal.ReplayLib.Models.Enums;

public enum BuildTargetType : byte
{
    /** Unknown build target. */
    Unknown,

    /** Game target. */
    Game,

    /** Server target. */
    Server,

    /** Client target. */
    Client,

    /** Editor target. */
    Editor,

    /** Program target. */
    Program,
}