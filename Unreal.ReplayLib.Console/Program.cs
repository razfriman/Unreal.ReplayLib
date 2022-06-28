using System.Text.Json;
using Microsoft.Extensions.Logging;
using Unreal.ReplayLib.Fortnite;

namespace Unreal.ReplayLib.Console;

public static class Program
{
    public static void Main()
    {
        var loggerFactory = LoggerFactory.Create((x) => x.AddConsole());
        var logger = loggerFactory.CreateLogger("Unreal.ReplayLib.Console");
        var reader = new FortniteReplayReader(logger);
        // var replay = reader.ReadReplay("/Users/raz/Desktop/chapter2_season6_10.replay");
        var replay = reader.ReadReplay("/Users/raz/Downloads/48bd5a029c264b2ba699a4b33fb18d95_14abc9c9576243b4bf8cdf86cca27a32.replay");
        var json = JsonSerializer.Serialize(replay, new JsonSerializerOptions()
        {
            WriteIndented = true
        });
        File.WriteAllText("/Users/raz/RiderProjects/Unreal.ReplayLib/Unreal.ReplayLib.Console/replay.json", json);
        logger.LogInformation($"Done: {replay.ParseTime}ms");
    }
}