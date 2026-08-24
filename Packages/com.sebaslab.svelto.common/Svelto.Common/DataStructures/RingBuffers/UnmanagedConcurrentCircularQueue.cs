using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Svelto.Utilities;

namespace Svelto.DataStructures
{
    // Avoid false sharing on hot counters.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    struct PaddedLong
    {
        [FieldOffset(0)] public long value;
    }
    /// <summary>
    /// Bounded MPMC lock-free ring queue (Vyukov-style) with a fixed unmanaged cell size (TCell).
    ///
    /// Key point for your use case:
    /// - Enqueue<K> copies only sizeof(K) bytes into the slot (not the full cell).
    /// - Dequeue<K> returns only sizeof(K) bytes from the slot (not the full cell).
    ///
    /// No "Allow unsafe code" required. Uses System.Runtime.CompilerServices.Unsafe.
    /// </summary>
    public sealed class UnmanagedConcurrentCircularQueue<TCell> where TCell : unmanaged
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Slot
        {
            public long sequence;
            public TCell cell;
        }

        readonly Slot[] buffer;
        readonly int mask;
        readonly int capacity;

        PaddedLong head;
        PaddedLong tail;

        public UnmanagedConcurrentCircularQueue(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException("size", "Must be positive");
            capacity = Utils.NextPowerOfTwo(size);
            mask = capacity - 1;

            buffer = new Slot[capacity];
            for (int i = 0; i < capacity; i++)
                buffer[i].sequence = i;

            head.value = 0;
            tail.value = 0;
        }

        public int Capacity => capacity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(in TCell value)
        {
            while (true)
            {
                long pos = Volatile.Read(ref tail.value);
                int index = (int)pos & mask;

                ref Slot slot = ref buffer[index];
                long seq = Volatile.Read(ref slot.sequence);
                long dif = seq - pos;

                if (dif == 0)
                {
                    if (Interlocked.CompareExchange(ref tail.value, pos + 1, pos) == pos)
                    {
                        slot.cell = value;
                        Volatile.Write(ref slot.sequence, pos + 1);
                        return true;
                    }

                    continue;
                }

                if (dif < 0)
                    return false; // full

                // another producer moved tail; retry
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(in TCell value, int size)
        {
            DebugAssertFits(size);

            while (true)
            {
                long pos = Volatile.Read(ref tail.value);
                int index = (int)pos & mask;

                ref Slot slot = ref buffer[index];
                long seq = Volatile.Read(ref slot.sequence);
                long dif = seq - pos;

                if (dif == 0)
                {
                    if (Interlocked.CompareExchange(ref tail.value, pos + 1, pos) == pos)
                    {
                        CopyIntoCell(ref slot.cell, value, size);
                        Volatile.Write(ref slot.sequence, pos + 1);
                        return true;
                    }

                    continue;
                }

                if (dif < 0)
                    return false; // full

                // another producer moved tail; retry
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out TCell value)
        {
            while (true)
            {
                long pos = Volatile.Read(ref head.value);
                int index = (int)pos & mask;

                ref Slot slot = ref buffer[index];
                long seq = Volatile.Read(ref slot.sequence);
                long dif = seq - (pos + 1);

                if (dif == 0)
                {
                    if (Interlocked.CompareExchange(ref head.value, pos + 1, pos) == pos)
                    {
                        value = slot.cell;
                        Volatile.Write(ref slot.sequence, pos + capacity);
                        return true;
                    }

                    continue;
                }

                if (dif < 0)
                {
                    value = default;
                    return false; // empty
                }

                // another consumer moved head; retry
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out TCell value, int count)
        {
            DebugAssertFits(count);

            while (true)
            {
                long pos = Volatile.Read(ref head.value);
                int index = (int)pos & mask;

                ref Slot slot = ref buffer[index];
                long seq = Volatile.Read(ref slot.sequence);
                long dif = seq - (pos + 1);

                if (dif == 0)
                {
                    if (Interlocked.CompareExchange(ref head.value, pos + 1, pos) == pos)
                    {
                        CopyOutOfCell(ref slot.cell, 0, out value, count);
                        Volatile.Write(ref slot.sequence, pos + capacity);
                        return true;
                    }

                    continue;
                }

                if (dif < 0)
                {
                    value = default;
                    return false; // empty
                }

                // another consumer moved head; retry
            }
        }

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
            ref byte src = ref Unsafe.Add(ref src0, byteOffset);
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
    }
}
