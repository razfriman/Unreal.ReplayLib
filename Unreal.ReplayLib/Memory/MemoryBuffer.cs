using System;
using System.IO;

namespace Unreal.ReplayLib.Memory
{
    public sealed unsafe class MemoryBuffer : IDisposable
    {
        public UnmanagedMemoryStream Stream { get; private set; }
        public int Size { get; private set; }
        public byte* PositionPointer { get; private set; }
        public Memory<byte> Memory { get; private set; }

        private readonly IPinnedMemoryOwner<byte> _pinnedOwner;

        public MemoryBuffer(byte* pointer, int length)
        {
            PositionPointer = pointer;
            Size = length;
        }

        public MemoryBuffer(int bytes)
        {
            _pinnedOwner = PinnedMemoryPool<byte>.Shared.Rent(bytes);
            Memory = _pinnedOwner.PinnedMemory.Memory;
            PositionPointer = (byte*)_pinnedOwner.PinnedMemory.Pointer;
            Size = bytes;
            Stream = new UnmanagedMemoryStream(PositionPointer, bytes, bytes, FileAccess.ReadWrite);
        }

        public void Dispose()
        {
            _pinnedOwner?.Dispose();
            Stream?.Dispose();
        }
    }
}