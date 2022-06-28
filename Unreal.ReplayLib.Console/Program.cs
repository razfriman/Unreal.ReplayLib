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
        var replay = reader.ReadReplay("/Users/raz/Desktop/chapter2_season6_10.replay");
        var json = JsonSerializer.Serialize(replay);
        System.Console.WriteLine(json);
        System.Console.WriteLine("Done");
    }
}