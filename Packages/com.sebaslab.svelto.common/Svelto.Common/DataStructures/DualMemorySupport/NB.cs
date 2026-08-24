#if DEBUG && !PROFILE_SVELTO
#define ENABLE_DEBUG_CHECKS
#endif

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Svelto.Common;

namespace Svelto.DataStructures
{
    sealed class NBDebugProxy<T> where T : struct
    {
        public NBDebugProxy(NB<T> array)
        {
            this._array = array;
        }

        public T[] Items
        {
            get
            {
                T[] array = new T[_array.capacity];

                _array.CopyTo(0, array, 0, (uint)_array.capacity);

                return array;
            }
        }

        NBInternal<T> _array;
    }

    /// <summary>
    /// NB stands for NativeBuffer
    /// 
    /// NativeBuffers were initially mainly designed to be used inside Unity Jobs. They wrap an EntityDB array of components
    /// but do not track it. Hence, it's meant to be used temporarily and locally as the array can become invalid
    /// after a submission of entities. However, they cannot be used as ref struct
    ///
    /// ------> NBs are wrappers of native arrays. Are not meant to resize or be freed
    ///
    /// NBs cannot have a count, because a count of the meaningful number of items is not tracked.
    /// Example: an NB could be initialized with a size 10 and count 0. Then the buffer is used to fill entities
    /// but the count will stay zero. It's not the NB responsibility to track the count
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [DebuggerTypeProxy(typeof(NBDebugProxy<>))]
    struct NBInternal<T> : IBuffer<T>where T : struct
    {
        /// <summary>
        /// Note: static constructors are NOT compiled by burst as long as there are no static fields in the struct
        /// </summary>
        static NBInternal()
        {
#if ENABLE_DEBUG_CHECKS
            if (TypeCache<T>.isUnmanaged == false)
                throw new Exception("NativeBuffer (NB) supports only unmanaged types");
#endif
        }

        public NBInternal(IntPtr array, uint capacity): this()
        {
            _ptr = array;
            _capacity = capacity;
        }

#if ENABLE_DEBUG_CHECKS
        internal readonly unsafe int* _rwState;
#endif

        public unsafe NBInternal(IntPtr array, uint capacity, IntPtr rwStatePtr): this()
        {
            _ptr = array;
            _capacity = capacity;
#if ENABLE_DEBUG_CHECKS
            _rwState = (int*)rwStatePtr;
#endif
        }

        public void CopyTo(uint sourceStartIndex, T[] destination, uint destinationStartIndex, uint count)
        {
            for (int i = 0; i < count; i++)
            {
                destination[i] = this[i];
            }
        }

        public void Clear()
        {
            MemoryUtilities.MemClear<T>(_ptr, _capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr ToNativeArray(out int capacity)
        {
            capacity = (int)_capacity;
            return _ptr;
        }

        public int capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)_capacity;
        }

        public bool isValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _ptr != IntPtr.Zero;
        }

        public ref T this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                unsafe
                {
#if ENABLE_DEBUG_CHECKS
                    if (index >= _capacity)
                        throw new Exception($"NativeBuffer - out of bound access: index {index} - capacity {capacity}");
#endif
                    return ref Unsafe.AsRef<T>((void*)(_ptr + (int)index * MemoryUtilities.SizeOf<T>()));
                }
            }
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                unsafe
                {
#if ENABLE_DEBUG_CHECKS
                    if (index < 0 || index >= _capacity)
                        throw new Exception($"NativeBuffer - out of bound access: index {index} - capacity {capacity}");
#endif
                    return ref Unsafe.AsRef<T>((void*)(_ptr + index * MemoryUtilities.SizeOf<T>()));
                }
            }
        }
        
        public static implicit operator NB<T>(NBInternal<T> proxy) => new NB<T>(proxy);
        public static implicit operator NBInternal<T>(NB<T> proxy) => new NBInternal<T>(proxy.ToNativeArray(out var capacity), (uint)capacity);

        readonly uint _capacity;

#if UNITY_COLLECTIONS || UNITY_JOBS || UNITY_BURST
#if UNITY_BURST
        [Unity.Burst.NoAlias]
#endif
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
#endif
        readonly IntPtr _ptr;
    }

    /// <summary>
    /// Note: this struct should be ref, however with jobs is a common pattern to use NB as a field of a struct. This pattern should be replaced
    /// with the introduction of new structs that can be hold but must be requested through some contracts like:
    /// AsReader, AsWriter, AsReadOnly, AsParallelReader, AsParallelWriter and so on. In this way NB can keep track about how the buffer is used
    /// and can throw exceptions if the buffer is used in the wrong way.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public ref struct NB<T> where T : struct
    {
        NBInternal<T> _bufferImplementation;

        internal NB(NBInternal<T> nbInternal)
        {
            _bufferImplementation = nbInternal;
        }

        public void CopyTo(uint sourceStartIndex, T[] destination, uint destinationStartIndex, uint count)
        {
            _bufferImplementation.CopyTo(sourceStartIndex, destination, destinationStartIndex, count);
        }

        public void Clear()
        {
            _bufferImplementation.Clear();
        }

        public int capacity => _bufferImplementation.capacity;

        public bool isValid => _bufferImplementation.isValid;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr ToNativeArray(out int capacity)
        {
            return _bufferImplementation.ToNativeArray(out capacity);
        }

        public ref T this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _bufferImplementation[index];
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if DEBUG && !PROFILE_SVELTO
                if (index >= _bufferImplementation.capacity)
                    throw new IndexOutOfRangeException("Paranoid check failed!");
#endif

                return ref _bufferImplementation[index];
            }
        }

#if ENABLE_DEBUG_CHECKS
        public unsafe Reader AsReader()
        {
            return new Reader(_bufferImplementation, new Sentinel(_bufferImplementation._rwState, Sentinel.readFlag));
        }

        public unsafe Writer AsWriter()
        {
            return new Writer(_bufferImplementation, new Sentinel(_bufferImplementation._rwState, Sentinel.writeFlag));
        }
#else
        public Reader AsReader() { return new Reader(_bufferImplementation, default); }
        public Writer AsWriter() { return new Writer(_bufferImplementation, default); }
#endif

        public static NB<T> Create(IntPtr array, int capacity, IntPtr rwStatePtr)
        {
#if ENABLE_DEBUG_CHECKS
            return new NB<T>(new NBInternal<T>(array, (uint)capacity, rwStatePtr));
#else
            return new NB<T>(new NBInternal<T>(array, (uint)capacity));
#endif
        }

        public ref struct Reader
        {
            NBInternal<T> _nb;
            readonly TestThreadSafety _guard;

            internal Reader(NBInternal<T> nb, Sentinel sentinel)
            {
                _nb = nb;
                _guard = sentinel.TestThreadSafety();
            }

            public int capacity => _nb.capacity;

            public ref readonly T this[uint index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _nb[index];
            }

            public ref readonly T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _nb[index];
            }

            public void Dispose()
            {
                _guard.Dispose();
            }
        }

        public ref struct Writer
        {
            NBInternal<T> _nb;
            readonly TestThreadSafety _guard;

            internal Writer(NBInternal<T> nb, Sentinel sentinel)
            {
                _nb = nb;
                _guard = sentinel.TestThreadSafety();
            }

            public int capacity => _nb.capacity;

            public ref T this[uint index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _nb[index];
            }

            public ref T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _nb[index];
            }

            public void Dispose()
            {
                _guard.Dispose();
            }
        }
    }
}

