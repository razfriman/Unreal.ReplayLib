using System;

namespace Unreal.ReplayLib.Memory
{
    public abstract class PinnedMemoryPool<T> : IDisposable
    {
        // Store the shared ArrayMemoryPool in a field of its derived sealed type so the Jit can "see" the exact type
        // when the Shared property is inlined which will allow it to devirtualize calls made on it.
        private static readonly PinnedArrayMemoryPool<T> SShared = new();

        public static PinnedMemoryPool<T> Shared => SShared;

        public abstract IPinnedMemoryOwner<T> Rent(int minBufferSize = -1);

        public abstract int MaxBufferSize { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);
    }
}