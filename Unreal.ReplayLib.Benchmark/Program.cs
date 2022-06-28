using BenchmarkDotNet.Running;

namespace Unreal.ReplayLib.Benchmark;

public class Program
{
    public static void Main()
    {
        BenchmarkRunner.Run<BenchmarkReadReplay>();
    }
}