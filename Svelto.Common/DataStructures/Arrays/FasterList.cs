#if DEBUG && !PROFILE_SVELTO
#define ENABLE_DEBUG_CHECKS
#endif

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DBC.Common;
using Svelto.Common;
using Svelto.DataStructures;

namespace Svelto.DataStructures
{
    //Note: the burst compatible version of a dynamic array is found in Svelto.ECS and is called NativeDynamicArray/Cast
    public class FasterList<T>
    {
        static readonly EqualityComparer<T> _comp = EqualityComparer<T>.Default;

        public FasterList()
        {
            _count = 0;

            _buffer = new T[0];
        }

        public FasterList(uint initialSize)
        {
            _count = 0;

            _buffer = new T[initialSize];
        }

        public FasterList(int initialSize) : this((uint)initialSize)
        {
        }

        public FasterList(params T[] collection)
        {
            _buffer = new T[collection.Length];

            Array.Copy(collection, _buffer, collection.Length);

            _count = (uint)collection.Length;
        }

        public FasterList(in ArraySegment<T> collection)
        {
            _buffer = new T[collection.Count];

            collection.CopyTo(_buffer, 0);

            _count = (uint)collection.Count;
        }
        
        public FasterList(Span<T> collection)
        {
            _buffer = new T[collection.Length];

            collection.CopyTo(_buffer);

            _count = (uint)collection.Length;
        }

        public FasterList(ICollection<T> collection)
        {
            _buffer = new T[collection.Count];

            collection.CopyTo(_buffer, 0);

            _count = (uint)collection.Count;
        }

        public FasterList(ICollection<T> collection, int extraSize)
        {
            _buffer = new T[(uint)collection.Count + (uint)extraSize];

            collection.CopyTo(_buffer, 0);

            _count = (uint)collection.Count;
        }

        public FasterList(in FasterList<T> source)
        {
            _buffer = new T[source.count];

            source.CopyTo(_buffer, 0);

            _count = (uint)source.count;
        }

        public FasterList(in FasterReadOnlyList<T> source)
        {
            _buffer = new T[source.count];

            source.CopyTo(_buffer, 0);

            _count = (uint)source.count;
        }

        public int count    => (int)_count;
        public int capacity => _buffer.Length;

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if ENABLE_DEBUG_CHECKS
                    if (index >= _count)
                        throw new Exception($"Fasterlist - out of bound access: index {index} - count {_count}");
#endif                
                return ref _buffer[(uint)index];
            }
        }

        public ref T this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if ENABLE_DEBUG_CHECKS
                    if (index >= _count)
                        throw new Exception($"Fasterlist - out of bound access: index {index} - count {_count}");
#endif                
                return ref _buffer[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FasterList<T> Add(in T item)
        {
            if (_count == _buffer.Length)
                AllocateMore();

            _buffer[_count++] = item;

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAt(uint location, in T item)
        {
            EnsureCountIsAtLeast(location + 1);

            _buffer[location] = item;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetOrCreate(uint location, in Func<T> item)
        {
            EnsureCountIsAtLeast(location + 1);

            if (_comp.Equals(this[location], default) == true)
                this[location] = item();

            return ref this[location];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FasterList<T> AddRange(in FasterList<T> items)
        {
            AddRange(items._buffer, (uint)items.count);

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FasterList<T> AddRange(in FasterReadOnlyList<T> items)
        {
            AddRange(items._list._buffer, (uint)items.count);

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(T[] items, uint count)
        {
            if (count == 0) return;

            if (_count + count > _buffer.Length)
                AllocateMore(_count + count);

            Array.Copy(items, 0, _buffer, _count, count);
            _count += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(T[] items)
        {
            AddRange(items, (uint)items.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T item)
        {
            for (uint index = 0; index < _count; index++)
                if (_comp.Equals(_buffer[index], item))
                    return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(_buffer, 0, array, arrayIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MemClear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (TypeCache<T>.isUnmanaged == false) 
                Array.Clear(_buffer, 0, _buffer.Length);

            _count = 0;
        }

        public static FasterList<T> Fill<U>(uint initialSize) where U : T, new()
        {
            var list = PreFill<U>(initialSize);

            list._count = initialSize;

            return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FasterListEnumerator<T> GetEnumerator()
        {
            return new FasterListEnumerator<T>(this, (uint)count);
        }

        public void IncreaseCapacityBy(uint increment)
        {
            IncreaseCapacityTo((uint)(_buffer.Length + increment));
        }

        public void IncreaseCapacityTo(uint newCapacity)
        {
            Check.Require(newCapacity > _buffer.Length);

            var newList = new T[newCapacity];
            if (_count > 0) Array.Copy(_buffer, newList, _count);
            _buffer = newList;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCountTo(uint newCount)
        {
            if (_buffer.Length < newCount)
                AllocateMore(newCount);

            _count = newCount;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCountIsAtLeast(uint newCount)
        {
            if (_buffer.Length < newCount)
                AllocateMore(newCount);

            if (_count < newCount)
                _count = newCount;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrementCountBy(uint increment)
        {
            var count = _count + increment;

            if (_buffer.Length < count)
                AllocateMore(count);

            _count = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InsertAt(uint index, in T item)
        {
            Check.Require(index <= _count, "out of bound index");

            if (_count == _buffer.Length) AllocateMore();

            Array.Copy(_buffer, index, _buffer, index + 1, _count - index);
            ++_count;

            _buffer[index] = item;
        }

        public static explicit operator FasterList<T>(T[] array)
        {
            return new FasterList<T>(array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T Peek()
        {
            return ref _buffer[_count - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T Pop()
        {
            --_count;
            return ref _buffer[_count];
        }

        /// <summary>
        ///     this is a dirtish trick to be able to use the index operator
        ///     before adding the elements through the Add functions
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="initialSize"></param>
        /// <returns></returns>
        public static FasterList<T> PreFill<U>(uint initialSize) where U : T, new()
        {
            var list = new FasterList<T>(initialSize);

            if (default(U) == null)
                for (var i = 0; i < initialSize; i++)
                    list._buffer[(uint)i] = new U();

            return list;
        }

        public static FasterList<T> PreInit(uint initialSize)
        {
            var list = new FasterList<T>(initialSize);

            list._count = initialSize;

            return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Push(in T item)
        {
            AddAt(_count, item);

            return _count - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(uint index)
        {
            Check.Require(index < _count, "out of bound index");

            if (index == --_count)
                return;

            Array.Copy(_buffer, index + 1, _buffer, index, _count - index);

            _buffer[_count] = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetToReuse()
        {
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(uint newSize)
        {
            if (newSize == _buffer.Length) return;

            Array.Resize(ref _buffer, (int)newSize);
        }

        public bool ReuseOneSlot<U>(out U result) where U : T
        {
            if (_count >= _buffer.Length)
            {
                result = default;

                return false;
            }

            if (default(U) == null)
            {
                result = (U)_buffer[_count];

                if (result != null)
                {
                    _count++;
                    return true;
                }

                return false;
            }

            _count++;
            result = default;
            return true;
        }

        public bool ReuseOneSlot<U>() where U : T
        {
            if (_count >= _buffer.Length)
                return false;

            _count++;

            return true;
        }

        public bool ReuseOneSlot()
        {
            if (_count >= _buffer.Length)
                return false;

            _count++;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ToArray()
        {
            var destinationArray = new T[_count];

            Array.Copy(_buffer, 0, destinationArray, 0, _count);

            return destinationArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Trim()
        {
            if (_count < _buffer.Length)
                Resize(_count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrimCount(uint newCount)
        {
            Check.Require(_count >= newCount, "the new length must be less than the current one");

            _count = newCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UnorderedRemoveAt(uint index)
        {
            Check.Require(index < _count && _count > 0, "out of bound index");

            if (index == --_count)
            {
                _buffer[_count] = default;
                return false;
            }

            _buffer[index]  = _buffer[_count];
            _buffer[_count] = default;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AllocateMore()
        {
            var newLength = (int)((_buffer.Length + 1) * 1.5f);
            var newList   = new T[newLength];
            if (_count > 0) Array.Copy(_buffer, newList, _count);
            _buffer = newList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //Note: maybe I should be sure that the count is always multiple of 4
        void AllocateMore(uint newSize)
        {
            Check.Require(newSize > _buffer.Length);
            var newLength = (int)(newSize * 1.5f);

            var newList = new T[newLength];
            if (_count > 0) Array.Copy(_buffer, newList, _count);
            _buffer = newList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AllocateTo(uint newSize)
        {
            Check.Require(newSize > _buffer.Length);

            var newList = new T[newSize];
            if (_count > 0) Array.Copy(_buffer, newList, _count);
            _buffer = newList;
        }

        internal T[]  _buffer;
        internal uint _count;
    }
 }
 
    
public static class NoVirt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] ToArrayFast<T>(this FasterList<T> fasterList, out int count)
    {
        count = (int)fasterList._count;

        return fasterList._buffer;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnorderedRemoveAt<T>(this List<T> list, int index)
    {
        list[index] = list[^1];
        list.RemoveAt(list.Count - 1);
    }
}
