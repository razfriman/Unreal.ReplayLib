namespace Unreal.ReplayLib.Models;

public class ReplayData
{
    public uint Start { get; set; }
    public uint End { get; set; }
    public int DecompressedLength { get; set; }
    public int CompressedLength { get; set; }
    public long Position { get; set; }
}