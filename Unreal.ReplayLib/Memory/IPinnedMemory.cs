using System;

namespace Unreal.ReplayLib.Memory
{
    public interface IPinnedMemoryOwner<T> : IDisposable
    {
        public PinnedMemory<T> PinnedMemory { get; }
    }
}