using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Unreal.ReplayLib.Memory
{
    //https://github.com/dotnet/runtime/blob/01b7e73cd378145264a7cb7a09365b41ed42b240/src/libraries/System.Private.CoreLib/src/System/Buffers/TlsOverPerCoreLockedStacksArrayPool.cs
    //
    internal sealed class PinnedArrayPool<T>
    {
        private const int NumBuckets = 20;

        private const int MaxPerCorePerArraySizeStacks = 64;

        private const int MaxBuffersPerArraySizePerCore = 8;

        private readonly int[] _bucketArraySizes;

        private readonly PerCoreLockedStacks[] _buckets = new PerCoreLockedStacks[NumBuckets];

        [ThreadStatic] private static PinnedMemory<T>[] _tTlsBuckets;

        private int _callbackCreated;

        private static readonly bool STrimBuffers = false;

        private static readonly ConditionalWeakTable<PinnedMemory<T>[], object> SAllTlsBuckets =
            STrimBuffers ? new ConditionalWeakTable<PinnedMemory<T>[], object>() : null;

        public PinnedArrayPool()
        {
            var sizes = new int[NumBuckets];
            for (var i = 0; i < sizes.Length; i++)
            {
                sizes[i] = Utilities.GetMaxSizeForBucket(i);
            }

            _bucketArraySizes = sizes;
        }

        private PerCoreLockedStacks CreatePerCoreLockedStacks(int bucketIndex)
        {
            var inst = new PerCoreLockedStacks();
            return Interlocked.CompareExchange(ref _buckets[bucketIndex], inst, null) ?? inst;
        }

        private int Id => GetHashCode();

        public PinnedMemory<T> Rent(int minimumLength)
        {
            // Arrays can't be smaller than zero.  We allow requesting zero-length arrays (even though
            // pooling such an array isn't valuable) as it's a valid length array, and we want the pool
            // to be usable in general instead of using `new`, even for computed lengths.
            if (minimumLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumLength));
            }
            else if (minimumLength == 0)
            {
                // No need to log the empty array.  Our pool is effectively infinite
                // and we'll never allocate for rents and never store for returns.
                return PinnedMemory<T>.Empty;
            }

            PinnedMemory<T> buffer;

            // Get the bucket number for the array length
            var bucketIndex = Utilities.SelectBucketIndex(minimumLength);

            // If the array could come from a bucket...
            if (bucketIndex < _buckets.Length)
            {
                // First try to get it from TLS if possible.
                var tlsBuckets = _tTlsBuckets;
                if (tlsBuckets != null)
                {
                    buffer = tlsBuckets[bucketIndex];
                    if (buffer != null)
                    {
                        tlsBuckets[bucketIndex] = null;

                        return buffer;
                    }
                }

                // We couldn't get a buffer from TLS, so try the global stack.
                var b = _buckets[bucketIndex];
                if (b != null)
                {
                    buffer = b.TryPop();
                    if (buffer != null)
                    {
                        return buffer;
                    }
                }

                // No buffer available.  Allocate a new buffer with a size corresponding to the appropriate bucket.
                var arrBuffer = GC.AllocateUninitializedArray<T>(_bucketArraySizes[bucketIndex]);

                buffer = new PinnedMemory<T>(arrBuffer);
            }
            else
            {
                // The request was for a size too large for the pool.  Allocate an array of exactly the requested length.
                // When it's returned to the pool, we'll simply throw it away.
                var arrBuffer = GC.AllocateUninitializedArray<T>(minimumLength);

                buffer = new PinnedMemory<T>(arrBuffer);
            }

            return buffer;
        }

        public void Return(PinnedMemory<T> array, bool clearArray = false)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            // Determine with what bucket this array length is associated
            var bucketIndex = Utilities.SelectBucketIndex(array.Length);

            // If we can tell that the buffer was allocated (or empty), drop it. Otherwise, check if we have space in the pool.
            var haveBucket = bucketIndex < _buckets.Length;
            if (haveBucket)
            {
                // Check to see if the buffer is the correct size for this bucket
                if (array.Length != _bucketArraySizes[bucketIndex])
                {
                    throw new ArgumentException("Buffer not from pool", nameof(array));
                }

                // Write through the TLS bucket.  If there weren't any buckets, create them
                // and store this array into it.  If there were, store this into it, and
                // if there was a previous one there, push that to the global stack.  This
                // helps to keep LIFO access such that the most recently pushed stack will
                // be in TLS and the first to be popped next.
                var tlsBuckets = _tTlsBuckets;
                if (tlsBuckets == null)
                {
                    _tTlsBuckets = tlsBuckets = new PinnedMemory<T>[NumBuckets];
                    tlsBuckets[bucketIndex] = array;
                    if (STrimBuffers)
                    {
                        SAllTlsBuckets.Add(tlsBuckets, null);

                        if (Interlocked.Exchange(ref _callbackCreated, 1) != 1)
                        {
                            Gen2GcCallback.Register(Gen2GcCallbackFunc, this);
                        }
                    }
                }
                else
                {
                    var prev = tlsBuckets[bucketIndex];
                    tlsBuckets[bucketIndex] = array;

                    if (prev != null)
                    {
                        var stackBucket = _buckets[bucketIndex] ?? CreatePerCoreLockedStacks(bucketIndex);
                        stackBucket.TryPush(prev);
                    }
                }
            }
            else
            {
                array.Dispose();
            }
        }

        public bool Trim()
        {
            var milliseconds = Environment.TickCount;
            var pressure = GetMemoryPressure();

            var perCoreBuckets = _buckets;
            for (var i = 0; i < perCoreBuckets.Length; i++)
            {
                perCoreBuckets[i]?.Trim((uint)milliseconds, Id, pressure, _bucketArraySizes[i]);
            }

            if (pressure == MemoryPressure.High)
            {
                foreach (var tlsBuckets in SAllTlsBuckets)
                {
                    var buckets = tlsBuckets.Key;

                    for (var i = 0; i < buckets.Length; i++)
                    {
                        buckets[i]?.Dispose();
                        buckets[i] = null;
                    }
                }
            }

            return true;
        }

        private static bool Gen2GcCallbackFunc(object target) => ((PinnedArrayPool<T>)(target)).Trim();

        private enum MemoryPressure
        {
            Low,
            Medium,
            High
        }

        private static MemoryPressure GetMemoryPressure()
        {
            const double highPressureThreshold = .90; // Percent of GC memory pressure threshold we consider "high"
            const double mediumPressureThreshold = .70; // Percent of GC memory pressure threshold we consider "medium"

            var memoryInfo = GC.GetGCMemoryInfo();
            if (memoryInfo.MemoryLoadBytes >= memoryInfo.HighMemoryLoadThresholdBytes * highPressureThreshold)
            {
                return MemoryPressure.High;
            }
            else if (memoryInfo.MemoryLoadBytes >= memoryInfo.HighMemoryLoadThresholdBytes * mediumPressureThreshold)
            {
                return MemoryPressure.Medium;
            }

            return MemoryPressure.Low;
        }

        private sealed class PerCoreLockedStacks
        {
            private readonly LockedStack[] _perCoreStacks;

            public PerCoreLockedStacks()
            {
                // Create the stacks.  We create as many as there are processors, limited by our max.
                var stacks = new LockedStack[Math.Min(Environment.ProcessorCount, MaxPerCorePerArraySizeStacks)];
                for (var i = 0; i < stacks.Length; i++)
                {
                    stacks[i] = new LockedStack();
                }

                _perCoreStacks = stacks;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryPush(PinnedMemory<T> array)
            {
                // Try to push on to the associated stack first.  If that fails,
                // round-robin through the other stacks.
                var stacks = _perCoreStacks;
                var index = Thread.GetCurrentProcessorId() % stacks.Length;
                for (var i = 0; i < stacks.Length; i++)
                {
                    if (stacks[index].TryPush(array))
                    {
                        return true;
                    }

                    if (++index == stacks.Length)
                    {
                        index = 0;
                    }
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public PinnedMemory<T> TryPop()
            {
                // Try to pop from the associated stack first.  If that fails,
                // round-robin through the other stacks.
                var stacks = _perCoreStacks;
                var index = Thread.GetCurrentProcessorId() % stacks.Length;
                for (var i = 0; i < stacks.Length; i++)
                {
                    PinnedMemory<T> arr;
                    if ((arr = stacks[index].TryPop()) != null)
                    {
                        return arr;
                    }

                    if (++index == stacks.Length)
                    {
                        index = 0;
                    }
                }

                return null;
            }

            public void Trim(uint tickCount, int id, MemoryPressure pressure, int bucketSize)
            {
                var stacks = _perCoreStacks;
                for (var i = 0; i < stacks.Length; i++)
                {
                    stacks[i].Trim(tickCount, pressure, bucketSize);
                }
            }
        }

        private sealed class LockedStack
        {
            private readonly PinnedMemory<T>[] _arrays = new PinnedMemory<T>[MaxBuffersPerArraySizePerCore];
            private int _count;
            private uint _firstStackItemMs;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryPush(PinnedMemory<T> array)
            {
                var enqueued = false;
                Monitor.Enter(this);
                if (_count < MaxBuffersPerArraySizePerCore)
                {
                    if (STrimBuffers && _count == 0)
                    {
                        // Stash the time the bottom of the stack was filled
                        _firstStackItemMs = (uint)Environment.TickCount;
                    }

                    _arrays[_count++] = array;
                    enqueued = true;
                }

                Monitor.Exit(this);
                return enqueued;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public PinnedMemory<T> TryPop()
            {
                PinnedMemory<T> arr = null;
                Monitor.Enter(this);
                if (_count > 0)
                {
                    arr = _arrays[--_count];
                    _arrays[_count] = null;
                }

                Monitor.Exit(this);
                return arr;
            }

            public void Trim(uint tickCount, MemoryPressure pressure, int bucketSize)
            {
                const uint stackTrimAfterMs = 60 * 1000; // Trim after 60 seconds for low/moderate pressure
                const uint stackHighTrimAfterMs = 10 * 1000; // Trim after 10 seconds for high pressure
                const uint stackRefreshMs = stackTrimAfterMs / 4; // Time bump after trimming (1/4 trim time)
                const int stackLowTrimCount = 1; // Trim one item when pressure is low
                const int stackMediumTrimCount = 2; // Trim two items when pressure is moderate
                const int stackHighTrimCount = MaxBuffersPerArraySizePerCore; // Trim all items when pressure is high
                const int
                    stackLargeBucket =
                        16384; // If the bucket is larger than this we'll trim an extra when under high pressure
                const int
                    stackModerateTypeSize = 16; // If T is larger than this we'll trim an extra when under high pressure
                const int
                    stackLargeTypeSize =
                        32; // If T is larger than this we'll trim an extra (additional) when under high pressure

                if (_count == 0)
                {
                    return;
                }

                var trimTicks = pressure == MemoryPressure.High ? stackHighTrimAfterMs : stackTrimAfterMs;

                lock (this)
                {
                    if (_count > 0 && _firstStackItemMs > tickCount || (tickCount - _firstStackItemMs) > trimTicks)
                    {
                        var trimCount = stackLowTrimCount;
                        switch (pressure)
                        {
                            case MemoryPressure.High:
                                trimCount = stackHighTrimCount;

                                // When pressure is high, aggressively trim larger arrays.
                                if (bucketSize > stackLargeBucket)
                                {
                                    trimCount++;
                                }

                                if (Unsafe.SizeOf<T>() > stackModerateTypeSize)
                                {
                                    trimCount++;
                                }

                                if (Unsafe.SizeOf<T>() > stackLargeTypeSize)
                                {
                                    trimCount++;
                                }

                                break;
                            case MemoryPressure.Medium:
                                trimCount = stackMediumTrimCount;
                                break;
                        }

                        while (_count > 0 && trimCount-- > 0)
                        {
                            var array = _arrays[--_count];

                            array.Dispose();

                            _arrays[_count] = null;
                        }

                        if (_count > 0 && _firstStackItemMs < uint.MaxValue - stackRefreshMs)
                        {
                            // Give the remaining items a bit more time
                            _firstStackItemMs += stackRefreshMs;
                        }
                    }
                }
            }
        }
    }

    internal static class Utilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SelectBucketIndex(int bufferSize) =>
            // Buffers are bucketed so that a request between 2^(n-1) + 1 and 2^n is given a buffer of 2^n
            // Bucket index is log2(bufferSize - 1) with the exception that buffers between 1 and 16 bytes
            // are combined, and the index is slid down by 3 to compensate.
            // Zero is a valid bufferSize, and it is assigned the highest bucket index so that zero-length
            // buffers are not retained by the pool. The pool will return the Array.Empty singleton for these.
            BitOperations.Log2((uint)bufferSize - 1 | 15) - 3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetMaxSizeForBucket(int binIndex)
        {
            var maxSize = 16 << binIndex;

            return maxSize;
        }
    }
}