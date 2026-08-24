using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Svelto.Common;

namespace Svelto.DataStructures.Native
{
    /// <summary>
    /// They are called strategy because they abstract the handling of the memory type used.
    /// Through the IBufferStrategy interface, with these, datastructure can use interchangeably native and managed memory and other strategies. 
    /// </summary>
    public struct NativeStrategy<T> : IBufferStrategy<T> where T : struct
    {
#if DEBUG && !PROFILE_SVELTO
        static NativeStrategy()
        {
            if (TypeCache<T>.isUnmanaged == false)
                throw new DBC.Common.PreconditionException("Only unmanaged data can be stored natively");
        }
#endif
        public NativeStrategy(uint size, Allocator allocator, bool clear = true) : this()
        {
            Alloc(size, allocator, clear);
        }

        public int       capacity           => _realBuffer.capacity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Alloc(uint newCapacity, Allocator allocator, bool memClear)
        {
#if DEBUG && !PROFILE_SVELTO
            if (!(this._realBuffer.ToNativeArray(out _) == IntPtr.Zero))
                throw new DBC.Common.PreconditionException("can't alloc an already allocated buffer");
            if (allocator != Allocator.Persistent && allocator != Allocator.Temp && allocator != Allocator.TempJob)
                throw new Exception("invalid allocator used for native strategy");
#endif
            _nativeAllocator = allocator;

            IntPtr   realBuffer = MemoryUtilities.NativeAlloc<T>(newCapacity, _nativeAllocator, memClear);
            NBInternal<T> b          = new NBInternal<T>(realBuffer, newCapacity);
            _invalidHandle = true;
            _realBuffer    = b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(uint newSize, bool copyContent = true, bool memClear = true)
        {
            if (newSize != capacity)
            {
                IntPtr pointer = _realBuffer.ToNativeArray(out _);
                pointer = MemoryUtilities.NativeRealloc<T>(pointer, newSize, _nativeAllocator
                                                   , newSize > capacity ? (uint) capacity : newSize
                                                   , copyContent, memClear);
                NBInternal<T> b = new NBInternal<T>(pointer, newSize);
                _realBuffer    = b;
                _invalidHandle = true;
            }
        }

        public IntPtr AsBytesPointer()
        {
            throw new NotImplementedException();
        }

        public void   SerialiseFrom(IntPtr bytesPointer)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftLeft(uint index, uint count)
        {
            DBC.Common.Check.Require(index < capacity, "out of bounds index");
            DBC.Common.Check.Require(count < capacity, "out of bounds count");

            if (count == index)
                return;

            DBC.Common.Check.Require(count > index, "wrong parameters used");

            var array = _realBuffer.ToNativeArray(out _);

            MemoryUtilities.MemMove<T>(array, index + 1, index, count - index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftRight(uint index, uint count)
        {
            DBC.Common.Check.Require(index < capacity, "out of bounds index");
            DBC.Common.Check.Require(count < capacity, "out of bounds count");

            if (count == index)
                return;

            DBC.Common.Check.Require(count > index, "wrong parameters used");

            var array = _realBuffer.ToNativeArray(out _);

            MemoryUtilities.MemMove<T>(array, index, index + 1, count - index);
        }

        public bool isValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _realBuffer.isValid;
        }

        public void FastClear() {}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => _realBuffer.Clear();

        public ref T this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _realBuffer[index];
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _realBuffer[index];
        }

        /// <summary>
        /// Note on the code of this method. Interfaces cannot be held by this structure as it must be used by Burst.
        /// This method could return directly _realBuffer, but this would cost of a boxing allocation.
        /// Using the GCHandle.Alloc I will occur to the boxing, but only once as long as the native handle is still
        /// valid
        /// </summary>
        /// <returns></returns>
#if UNITY_BURST 
        [Unity.Burst.BurstDiscard]
#endif        
        IBuffer<T> IBufferStrategy<T>.ToBuffer()
        {
            //handle has been invalidated, dispose of the hold GCHandle (if exists)
            if (_invalidHandle == true && ((IntPtr) _cachedReference != IntPtr.Zero))
            {
                _cachedReference.Free();
                _cachedReference = default;
            }

            _invalidHandle = false;
            if (((IntPtr) _cachedReference == IntPtr.Zero))
            {
                    _cachedReference = GCHandle.Alloc(_realBuffer, GCHandleType.Normal);
            }

            return (IBuffer<T>) _cachedReference.Target;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NB<T> ToRealBuffer()
        {
            return _realBuffer;
        }

        public void Dispose()
        {
            ReleaseCachedReference();

            if (_realBuffer.ToNativeArray(out _) != IntPtr.Zero)
                MemoryUtilities.NativeFree(_realBuffer.ToNativeArray(out _), _nativeAllocator);
            else
                throw new Exception("trying to dispose disposed buffer");

            _cachedReference = default;
            _realBuffer      = default;
        }

#if UNITY_BURST 
        [Unity.Burst.BurstDiscard]
#endif        
        void ReleaseCachedReference()
        {
            if ((IntPtr)_cachedReference != IntPtr.Zero)
                _cachedReference.Free();
        }

        Allocator _nativeAllocator;
        NBInternal<T>      _realBuffer;
        bool       _invalidHandle;

#if UNITY_COLLECTIONS || UNITY_JOBS || UNITY_BURST
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
#endif
        //NativeStrategy must stay unmanaged so it cannot hold on an IBuffer reference
        GCHandle _cachedReference;
    }
}