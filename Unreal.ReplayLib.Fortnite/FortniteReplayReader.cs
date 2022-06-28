using Microsoft.Extensions.Logging;

namespace Unreal.ReplayLib.Fortnite;

public class FortniteReplayReader : ReplayReader<FortniteReplay>
{
    public FortniteReplayReader(ILogger logger) : base(logger)
    {
    }
}