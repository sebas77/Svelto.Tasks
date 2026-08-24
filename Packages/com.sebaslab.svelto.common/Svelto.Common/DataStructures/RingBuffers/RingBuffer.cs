// True ring buffer (uses ALL slots) with overwrite-on-full semantics.
// producer == consumer can mean EMPTY or FULL; disambiguated by _count.
//
// Behaviour:
// - Enqueue overwrites oldest when full (drops 1 by advancing consumer)
// - Dequeue throws when empty
// - count is O(1)

using System;
using Svelto.Utilities;

namespace Svelto.DataStructures
{
    public class RingBuffer<T>
    {
        public RingBuffer(int capacity)
        {
            //todo: probably better to move to prime and fasts mod
            capacity = Utils.NextPowerOfTwo(capacity);
            _entries = new T[capacity];
            _modMask = capacity - 1;
        }

        public int capacity => _entries.Length;
        public int count => _count;

        public RingBufferEnumerator GetEnumerator()
        {
            return new RingBufferEnumerator(this);
        }

        public void CopyTo(Span<T> destination)
        {
            int c = _count;
            if (c == 0)
                return;

            GetReadSegments(c, out var first, out var second);

            first.CopyTo(destination);
            if (second.Length > 0)
                second.CopyTo(destination.Slice(first.Length));
        }

        public void AddTo(ref SpanList<T> result)
        {
            int c = _count;
            if (c == 0)
                return;

            GetReadSegments(c, out var first, out var second);

            result.AddRange(first);
            if (second.Length > 0)
                result.AddRange(second);
        }

        public void Reset()
        {
            _consumerCursor = _producerCursor = 0;
            _count = 0;
        }

        public ref T Dequeue()
        {
            if (_count == 0)
                throw new Exception("Buffer is empty");

            var consumerCursor = _consumerCursor;

            _consumerCursor = (uint)((_consumerCursor + 1) & _modMask);
            _count--;

            return ref _entries[consumerCursor];
        }

        // Overwrite-on-full enqueue: if full, drop oldest by advancing consumer.
        public void Enqueue(in T item)
        {
            if (_count == _entries.Length)
            {
                // drop oldest
                _consumerCursor = (uint)((_consumerCursor + 1) & _modMask);
                // _count stays at capacity
            }
            else
            {
                _count++;
            }

            _entries[_producerCursor] = item;

            _producerCursor = (uint)((_producerCursor + 1) & _modMask);
        }

        void GetReadSegments(int c, out Span<T> first, out Span<T> second)
        {
            // logical layout: c items starting at consumer, wrapping as needed
            int start = (int)_consumerCursor;

            int firstLen = Math.Min(_entries.Length - start, c);
            first = _entries.AsSpan(start, firstLen);

            int remaining = c - firstLen;
            second = remaining > 0 ? _entries.AsSpan(0, remaining) : Span<T>.Empty;
        }

        readonly int _modMask;
        readonly T[] _entries;

        uint _consumerCursor = 0;
        uint _producerCursor = 0;

        int _count = 0;

        public struct RingBufferEnumerator
        {
            readonly RingBuffer<T> _ringBuffer;
            readonly int _modMask;

            uint _index;
            int _remaining;
            bool _started;

            public RingBufferEnumerator(RingBuffer<T> ringBuffer)
            {
                _ringBuffer = ringBuffer;
                _modMask = _ringBuffer._modMask;

                _index = _ringBuffer._consumerCursor;
                _remaining = _ringBuffer._count; // snapshot at enumeration start
                _started = false;
            }

            public ref T Current => ref _ringBuffer._entries[_index];

            public bool MoveNext()
            {
                if (_remaining <= 0)
                    return false;

                if (_started == false)
                {
                    _started = true;
                    _remaining--;
                    return true;
                }

                _index = (uint)((_index + 1) & _modMask);
                _remaining--;
                return true;
            }
        }
    }
}
