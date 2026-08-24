using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Svelto.Utilities;

namespace Svelto.DataStructures
{
    /// <summary>
    /// Non-concurrent ring buffer (single-threaded) with fixed unmanaged cell size (TCell).
    ///
    /// - Enqueue<K> copies only sizeof(K) bytes into the slot.
    /// - Dequeue<K> returns only sizeof(K) bytes from the slot.
    /// - Enumerator iterates over full TCells currently in the queue (from Head to Tail).
    /// </summary>
    public sealed class UnmanagedCircularQueue<TCell> : IEnumerable<TCell> where TCell : unmanaged
    {
        readonly TCell[] buffer;
        readonly int     mask;
        readonly int     capacity;

        long head;
        long tail;

        public UnmanagedCircularQueue(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException("size", "Must be positive");
            capacity = Utils.NextPowerOfTwo(size);
            mask = capacity - 1;

            buffer = new TCell[capacity];
            head     = 0;
            tail     = 0;
        }

        public int Capacity => capacity;
        public int Count    => (int)(tail - head);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            head = 0;
            tail = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(in TCell value)
        {
            if (tail - head >= capacity)
                return false; // full

            int index = (int)(tail & mask);
            buffer[index] = value;
            
            tail++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(in TCell value, int count)
        {
            DebugAssertFits(count);

            if (tail - head >= capacity)
                return false; // full

            int index = (int)(tail & mask);
            ref TCell slot = ref buffer[index];
            
            CopyIntoCell(ref slot, value, count);
            
            tail++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out TCell value)
        {
            if (tail - head <= 0)
            {
                value = default;
                return false; // empty
            }

            int index = (int)(head & mask);
            value = buffer[index];

            head++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out TCell value, int count)
        {
            DebugAssertFits(count);

            if (tail - head <= 0)
            {
                value = default;
                return false; // empty
            }

            int index = (int)(head & mask);
            ref TCell slot = ref buffer[index];

            CopyOutOfCell(ref slot, 0, out value, count);

            head++;
            return true;
        }

        // --- Byte Copy Helpers ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CopyIntoCell(ref TCell cell, TCell value, int bytes)
        {
            ref byte dst0 = ref Unsafe.As<TCell, byte>(ref cell);
            ref byte src0 = ref Unsafe.As<TCell, byte>(ref value);

            Unsafe.CopyBlockUnaligned(ref dst0, ref src0, (uint)bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CopyOutOfCell(ref TCell cell, int byteOffset, out TCell value, int bytes)
        {
            value = default;

            ref byte src0 = ref Unsafe.As<TCell, byte>(ref cell);
            ref byte src  = ref Unsafe.Add(ref src0, byteOffset);
            ref byte dst0 = ref Unsafe.As<TCell, byte>(ref value);

            Unsafe.CopyBlockUnaligned(ref dst0, ref src, (uint)bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("DEBUG")]
        static void DebugAssertFits(int bytes)
        {
            int cellSize = Unsafe.SizeOf<TCell>();
            Debug.Assert((uint)bytes <= (uint)cellSize,
                $"count={bytes} must be <= sizeof({typeof(TCell).Name})={cellSize}.");
        }

        // --- Enumerator Support ---

        public struct Enumerator : IEnumerator<TCell>
        {
            readonly TCell[] _buffer;
            readonly int     _mask;
            readonly long    _tail;
            long             _currentPos;

            internal Enumerator(UnmanagedCircularQueue<TCell> rb)
            {
                _buffer     = rb.buffer;
                _mask       = rb.mask;
                _tail       = rb.tail;
                _currentPos = rb.head - 1; // Start before head
            }

            public bool MoveNext()
            {
                _currentPos++;
                return _currentPos < _tail;
            }

            public void Reset() => throw new NotSupportedException();

            // Pattern-based ref foreach expects a ref-returning Current
            public ref TCell Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _buffer[(int)(_currentPos & _mask)];
            }

            // IEnumerable/IEnumerator compatibility (value-returning Current)
            TCell IEnumerator<TCell>.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _buffer[(int)(_currentPos & _mask)];
            }

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _buffer[(int)(_currentPos & _mask)];
            }

            public void Dispose() { }
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        // Support `ref foreach` pattern directly via Enumerator.Current (by ref).
        // Keep RefEnumerator for backward compatibility with existing call sites.
        public struct RefEnumerator
        {
            Enumerator _e;

            internal RefEnumerator(UnmanagedCircularQueue<TCell> rb) => _e = new Enumerator(rb);

            // Allow foreach over the RefEnumerator value (pattern requires GetEnumerator on the expression type)
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RefEnumerator GetEnumerator() => this;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => _e.MoveNext();

            public ref TCell Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _e.Current;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RefEnumerator GetRefEnumerator() => new RefEnumerator(this);

        IEnumerator<TCell> IEnumerable<TCell>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
