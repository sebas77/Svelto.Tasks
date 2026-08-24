using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Svelto.Utilities;

namespace Svelto.DataStructures
{
// MonotonicWindowBuffer<T>
// ---------------------------------
// SPSC (single producer / single consumer) fixed-capacity sliding window indexed by a monotonically
// increasing int "logical index". The producer may publish indices out-of-order (e.g. set 8 before 5).
//
// IMPORTANT: T must be safe to publish/read across threads with no additional synchronization.
// In practice that means T should be immutable after Set(), or otherwise thread-safe for concurrent
// producer write + consumer read.
//
// Queue semantics are preserved by the consumer consuming strictly in-order: TryPeek/TryDequeue only
// operate on the current head index. Therefore if there is a "hole" (e.g. head=5 not published yet),
// the consumer will stall even if later indices (6,7,8,...) are already present.
//
// Operations are O(1) and non-blocking; readiness is tracked per slot via a PublishedIndex marker.
// Publication order is "write Value, then Volatile.Write(PublishedIndex)" so the consumer won’t
// observe an index as present before its value is visible (release/acquire pattern).

    public enum MonotonicSlotState 
    {
        NotPublished,
        Published,
        Consumed,
        OutOfRange,
        NotInitialised
    }
    
    public sealed class MonotonicWindowBuffer<T>
    {
        // Combined buffer for cache locality. published marker: slot contains logical index i if published == i + 1.
        // (i + 1 so default 0 means "not published")
        readonly (T value, int published)[] _buffer;

        readonly uint _capacity;
        readonly uint _mask;
        readonly uint _maxWindowSize;

        // Consumer-owned: next logical index to dequeue/peek.
        // -1 means "head not set".
        int _head;

        // Highest published index so far. -1 means nothing published yet.
        int _highestPublished;

        public int HighestPublishedIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _highestPublished);
        }

        public MonotonicWindowBuffer(uint maxWindowSize)
        {
            if (maxWindowSize == 0)
                throw new ArgumentOutOfRangeException(nameof(maxWindowSize));

            _maxWindowSize = maxWindowSize;

            _capacity = Utils.NextPowerOfTwo(maxWindowSize);
            _mask = _capacity - 1;

            int len = checked((int)_capacity);
            _buffer = new (T, int)[len];
            _head = -1;
            _highestPublished = -1;
        }

        // Returns the span from head to highest published index (inclusive), or 0 if nothing ready.
        // WARNING: this is NOT the number of dequeue-able items. Holes (unpublished indices) are counted.
        // Example: SetHead(0), Set(2, v) → Count returns 3, but TryDequeue fails (index 0 not published).
        // Do NOT use "if (Count > 0) TryDequeue()" — use TryDequeue() directly, it handles holes safely.
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int head = Volatile.Read(ref _head);
                int highest = Volatile.Read(ref _highestPublished);
                
                if (head == -1 || highest == -1 || highest < head)
                    return 0;
                
                return highest - head + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int index, in T value)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            int head = Volatile.Read(ref _head);
            
            if (head == -1)
                throw new MonotonicWindowBufferOverflowException("Trying to add elements before setting the head");

            // If head isn't set yet, this is an unsafe set: no window checks can be performed.
            if (head != -1)
            {
                // Do nothing if retired or outside window (it's older than the last processed)
                if (index < head) 
                    return false;

                if ((uint)(index - head) >= _maxWindowSize)
                    throw new MonotonicWindowBufferOverflowException($"Index {index} is outside of the window (head={head}, expectedCount={_maxWindowSize}).");
            }

            int slot = (int)((uint)index & _mask); //Modulo
            ref var valueTuple = ref _buffer[slot];
            
            int expectedMarker = index + 1;
            if (Volatile.Read(ref valueTuple.published) != expectedMarker)
            {
                valueTuple.value = value;
                Volatile.Write(ref valueTuple.published, expectedMarker); //must be after setting the value
                
                // Update highest published index (SPSC safe - we are the only writer).
                if (index > _highestPublished)
                    Volatile.Write(ref _highestPublished, index);
            }
            
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary>
        /// Consumer-only. Try get value at index if (and only if) published. Does not retire.
        ///
        public MonotonicSlotState TryGet(int index, out T value)
        {
            if (index < 0)
            {
                value = default;
                return MonotonicSlotState.OutOfRange;
            }
            
            int head = Volatile.Read(ref _head); //this is not thread safe, but doesn't matter if we get a retired index, published will be the real gate

            if (head == -1)
            {
                value = default;
                return MonotonicSlotState.NotInitialised;
            }

            if (index < head)
            {
                value = default;
                return MonotonicSlotState.Consumed;
            }

            if ((uint)(index - head) >= _maxWindowSize)
                throw new MonotonicWindowBufferOverflowException($"Index {index} is outside of the window (head={head}, expectedCount={_maxWindowSize}).");

            int slot = (int)((uint)index & _mask);
            
            ref var valueTuple = ref _buffer[slot];
            if (Volatile.Read(ref valueTuple.published) != (uint)index + 1)
            {
                value = default;
                return MonotonicSlotState.NotPublished;
            }
            value = valueTuple.value;

            return MonotonicSlotState.Published;
        }
        
        /// <summary>
        /// Consumer-only. Peek current head if (and only if) head has been published. Does not retire.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T value)
        {
            int head = _head;
            if (head == -1)
                throw new InvalidOperationException("Head not set.");

            uint headU = (uint)head;
            int slot = (int)(headU & _mask);

            ref var valueTuple = ref _buffer[slot];
            if (Volatile.Read(ref valueTuple.published) != headU + 1)
            {
                value = default;
                return false;
            }

            value = valueTuple.value;
            return true;
        }

        /// <summary>
        /// Consumer-only. Dequeue (retire) current head if published.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T value)
        {
            int head = _head;
            if (head == -1)
                throw new InvalidOperationException("Head not set.");

            uint headU = (uint)head;
            int slot = (int)(headU & _mask);

            ref var valueTuple = ref _buffer[slot];
            if (Volatile.Read(ref valueTuple.published) != headU + 1)
            {
                value = default!;
                return false;
            }

            value = valueTuple.value;
            
            ///ATTENTION: YES THE VALUE WILL LEAK HERE IF IT'S A CLASS, BUT ONLY UNTIL IT WILL BE OVERWRITTEN OR THE
            ///DATASTRUCTURE RELEASED. ADDING THE CLEAR OF THE VALUE HERE WOULD HAVE FORCED TO USE (SPIN)LOCKS AND IT'S NOT WORTH IT
            
            // Retire head (release).
            Volatile.Write(ref _head, head + 1);
       
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// Consumer-only. 
        public void SetHead(int newHead)
        {
            if (newHead < 0)
                throw new ArgumentOutOfRangeException(nameof(newHead));

            // Consumer-only. New head must not be retired and must be >= current head.
            int currentHead = _head;
            if (currentHead != -1 && newHead < currentHead)
                throw new InvalidOperationException("New head is retired (cannot move head backwards).");

            Volatile.Write(ref _head, newHead);
        }
    }

    public class MonotonicWindowBufferOverflowException : Exception
    {
        public MonotonicWindowBufferOverflowException(string message) : base(message)
        {
        }
    }
}
