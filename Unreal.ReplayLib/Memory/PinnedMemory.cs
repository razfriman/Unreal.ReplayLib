using System;
using System.Buffers;

namespace Unreal.ReplayLib.Memory
{
    public unsafe class PinnedMemory<T> : IDisposable
    {
        public static readonly PinnedMemory<T> Empty = new();

        public Memory<T> Memory { get; private set; }
        public MemoryHandle Handle { get; private set; }
        public void* Pointer { get; private set; }
        public int Length { get; private set; }

        private PinnedMemory() => Memory = Memory<T>.Empty;

        public PinnedMemory(T[] bytes)
        {
            Memory = new Memory<T>(bytes);
            Handle = Memory.Pin();
            Pointer = Handle.Pointer;
            Length = bytes.Length;
        }

        public void Dispose()
        {
            Handle.Dispose();
            Memory = null;
            Handle = new MemoryHandle();
            Pointer = Handle.Pointer;
        }

    }
}