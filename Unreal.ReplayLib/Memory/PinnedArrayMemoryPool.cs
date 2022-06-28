namespace Unreal.ReplayLib.Memory
{
    internal sealed partial class PinnedArrayMemoryPool<T> : PinnedMemoryPool<T>
    {
        private const int MaximumBufferSize = int.MaxValue;

        public override int MaxBufferSize => MaximumBufferSize;

        public override IPinnedMemoryOwner<T> Rent(int minimumBufferSize = -1) =>
            new ArrayMemoryPoolBuffer(minimumBufferSize);

        protected override void Dispose(bool disposing)
        {
        }
    }

    internal sealed partial class PinnedArrayMemoryPool<T>
    {
        private sealed class ArrayMemoryPoolBuffer : IPinnedMemoryOwner<T>
        {
            private PinnedMemory<T> _array;
            private static readonly PinnedArrayPool<T> Pool = new();

            public PinnedMemory<T> PinnedMemory => _array;

            public ArrayMemoryPoolBuffer(int size) => _array = Pool.Rent(size);

            public void Dispose()
            {
                var array = _array;

                if (array != null)
                {
                    _array = null;
                    Pool.Return(array);
                }
            }
        }
    }
}