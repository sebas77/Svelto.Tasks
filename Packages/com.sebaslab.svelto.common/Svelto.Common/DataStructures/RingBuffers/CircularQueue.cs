//Note the Queue works exactly like a Ring, the only difference is that Queue can grow
//In fact I would have used Queue instead, but I need the Span support

using System;
using Svelto.Utilities;

namespace Svelto.DataStructures
{
    public class CircularQueue<T>
    {
        public CircularQueue(int capacity)
        {
            //todo: probably better to move to prime and fasts mod
            capacity = Utils.NextPowerOfTwo(capacity);
            _entries = new T[capacity];
        }

        public int capacity => _entries.Length;
        public int count
        {
            get
            {
                uint prod = _producerCursor;
                uint cons = _consumerCursor;
                if (prod >= cons)
                    return (int)(prod - cons);
                return _entries.Length - (int)(cons - prod);
            }
        }
        
        public RingBufferEnumerator GetEnumerator()
        {
            return new RingBufferEnumerator(this);   
        }
        
        public void CopyTo(Span<T> destination)
        {
            if (_producerCursor == _consumerCursor)
                return;
            
            if (_producerCursor > _consumerCursor) //no wrap
            {
                _entries.AsSpan((int)_consumerCursor, count).CopyTo(destination);
            }
            else
            {
                var start = (int)_consumerCursor;
                int end = (int)_producerCursor;
                var firstPart = _entries.AsSpan(start);
                var secondPart = _entries.AsSpan(0, end);
                firstPart.CopyTo(destination);
                secondPart.CopyTo(destination.Slice(firstPart.Length));
            }
        }
        
        public void AddTo(ref SpanList<T> result)
        {
            if (_producerCursor == _consumerCursor)
                return;
            
            if (_producerCursor > _consumerCursor) //no wrap
            {
                result.AddRange(_entries.AsSpan((int)_consumerCursor, count));
            }
            else
            {
                var start = (int)_consumerCursor;
                int end = (int)_producerCursor;
                var firstPart = _entries.AsSpan(start);
                var secondPart = _entries.AsSpan(0, end);
                
                result.AddRange(firstPart);
                result.AddRange(secondPart);
            }
        }
        
        public void Reset()
        {
            _consumerCursor = _producerCursor = 0;
        }

        public ref T Dequeue()
        {
            if (_consumerCursor == _producerCursor)
                throw new Exception("Queue is empty");
            
            var consumerCursor = _consumerCursor;
            
            _consumerCursor = (uint) ((_consumerCursor + 1) & _modMask);
            
            return ref _entries[consumerCursor];
        }

        public void Enqueue(in T item)
        {
            var next = (uint)((_producerCursor + 1) & _modMask);
            if (next == _consumerCursor)
                throw new Exception("Queue is full");
            
            _entries[_producerCursor] = item;
            
            _producerCursor = next;
        }

        int  _modMask => _entries.Length - 1;
        
        readonly T[]        _entries;
        
        uint _consumerCursor = 0;
        uint _producerCursor = 0;
        
        public struct RingBufferEnumerator
        {
            readonly CircularQueue<T> _circularQueue;
            readonly uint _end;
            readonly int _modMask;
            uint _index;
            bool _started;

            public RingBufferEnumerator(CircularQueue<T> circularQueue)
            {
                _circularQueue = circularQueue;
                _index = _circularQueue._consumerCursor;
                _end = _circularQueue._producerCursor;
                _modMask = _circularQueue._modMask;
                _started = false;
            }

            public ref T Current => ref _circularQueue._entries[_index];

            public bool MoveNext()
            {
                if (_started == false)
                {
                    _started = true;
                    return _index != _end;
                }
                
                _index = (uint)((_index + 1) & _modMask);
                return _index != _end;
            }
        }
    }
}