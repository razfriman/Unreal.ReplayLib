namespace Unreal.ReplayLib.Models.Enums;

public enum BuildConfiguration : byte
{
    /** Unknown build configuration. */
    Unknown,

    /** Debug build. */
    Debug,

    /** DebugGame build. */
    DebugGame,

    /** Development build. */
    Development,

    /** Shipping build. */
    Shipping,

    /** Test build. */
    Test
}