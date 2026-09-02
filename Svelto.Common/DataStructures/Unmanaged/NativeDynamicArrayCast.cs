using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Svelto.Common;

namespace Svelto.DataStructures
{
    public struct NativeDynamicArrayCast<T>:IDisposable where T : struct
    {
        public NativeDynamicArrayCast(uint size, Allocator allocator)
        {
            _array = NativeDynamicArray.Alloc<T>(allocator, size);
        }
        public NativeDynamicArrayCast(NativeDynamicArray array) : this() { _array = array; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Count() => _array.Count<T>();

        public int count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.Count<T>();
        }
        
        public int capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.Capacity<T>();
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _array.Get<T>((uint) index);
        }

        public ref T this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _array.Get<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T id) { _array.Add(id); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnorderedRemoveAt(uint index) { _array.UnorderedRemoveAt<T>(index); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(uint index) { _array.RemoveAt<T>(index); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() { _array.Clear(); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { _array.Dispose(); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AddAt(uint lastIndex) { return ref _array.AddAt<T>(lastIndex); }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(uint newSize) { _array.Resize<T>(newSize); }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeDynamicArray ToNativeArray() { return _array; }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(uint index, in T value)
        {
            _array.Set(index, value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, in T value)
        {
            _array.Set((uint)index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(in T value)
        {
            for (uint index = 0; index < Count(); index++)
                if (_comp.Equals(this[index], value))
                    return true;

            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(in T value)
        {
            for (uint index = 0; index < Count(); index++)
                if (_comp.Equals(this[index], value))
                {
                    RemoveAt(index);
                    return;
                }
        }
        
        static readonly EqualityComparer<T> _comp = EqualityComparer<T>.Default;

        public bool isValid => _array.isValid;

        NativeDynamicArray _array;
    }
}