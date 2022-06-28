using Unreal.ReplayLib.Compression.OozSharp;

namespace Unreal.ReplayLib.Compression
{
    public unsafe class Oodle
    {
        private static readonly Kracken Kraken = new();

        public static void DecompressReplayData(byte* buffer, int bufferLength, byte* uncompressedBuffer,
            int uncompressedSize)
        {
            Kraken.Decompress(buffer, bufferLength, uncompressedBuffer, uncompressedSize);
        }
    }
}