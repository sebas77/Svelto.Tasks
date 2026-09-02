using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Svelto.ECS")]

namespace Svelto.DataStructures
{
    /// <summary>
    /// MB stands for ManagedBuffer
    ///
    /// MBs are not meant to be resized or freed. They are wrappers of constant size arrays.
    /// MBs always wrap external arrays, they are not meant to allocate memory by themselves.
    ///
    /// MB are wrappers of arrays. Are not meant to resize or free
    /// MBs cannot have a count, because a count of the meaningful number of items is not tracked.
    /// Example: an MB could be initialized with a size 10 and count 0. Then the buffer is used to fill entities
    /// but the count will stay zero. It's not the MB responsibility to track the count
    /// </summary>
    /// <typeparam name="T"></typeparam>
    struct MBInternal<T>:IBuffer<T> 
    {
        public MBInternal(T[]  array) : this()
        {
            _buffer = array;
        }
        
        public void Set(T[] array)
        {
            _buffer = array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(T[] collection, uint actualSize)
        {
            Array.Copy(collection, 0, _buffer, 0, actualSize);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(uint sourceStartIndex, T[] destination, uint destinationStartIndex, uint count)
        {
            Array.Copy(_buffer, sourceStartIndex, destination, destinationStartIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ToManagedArray()
        {
            return _buffer;
        }
        
        public static implicit operator MB<T>(MBInternal<T> proxy) => new MB<T>(proxy);
        public static implicit operator MBInternal<T>(MB<T> proxy) => new MBInternal<T>(proxy.ToManagedArray());
        
        public int capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length;
        }
        public bool isValid => _buffer != null;
        
        public ref T this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref _buffer[index];
            }
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref _buffer[index];
            }
        }
        
        T[]         _buffer;
    }

    public ref struct MB<T>
    {
        MBInternal<T> _bufferImplementation;

#if DEBUG && !PROFILE_SVELTO
        int _rwState;
#endif

        internal MB(MBInternal<T> mbInternal):this()
        {
            _bufferImplementation = mbInternal;
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
        
        public void Set(T[] array)
        {
            _bufferImplementation.Set(array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(T[] collection, uint actualSize)
        {
            _bufferImplementation.CopyFrom(collection, actualSize);
        }
        
        /// <summary>
        /// todo: replace public raw-array access with explicit reader/writer contracts as described in
        /// PROPOSAL_Controlled_MB_Buffer_Access.md. The backing array must eventually become framework-internal.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ToManagedArray()
        {
            return  _bufferImplementation.ToManagedArray();
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
                return ref _bufferImplementation[index];
            }
        }

#if DEBUG && !PROFILE_SVELTO
        public unsafe Reader AsReader()
        {
            fixed (int* p = &_rwState)
                return new Reader(_bufferImplementation, new Sentinel(p, Sentinel.readFlag));
        }

        public unsafe Writer AsWriter()
        {
            fixed (int* p = &_rwState)
                return new Writer(_bufferImplementation, new Sentinel(p, Sentinel.writeFlag));
        }
#else
        public Reader AsReader() { return new Reader(_bufferImplementation, default); }
        public Writer AsWriter() { return new Writer(_bufferImplementation, default); }
#endif

        public static MB<T> Create(T[] array)
        {
            return new MB<T>(new MBInternal<T>(array));
        }

        public ref struct Reader
        {
            MBInternal<T> _mb;
            readonly TestThreadSafety _guard;

            internal Reader(MBInternal<T> mb, Sentinel sentinel)
            {
                _mb = mb;
                _guard = sentinel.TestThreadSafety();
            }

            public int capacity => _mb.capacity;

            public ref T this[uint index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _mb[index];
            }

            public ref T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _mb[index];
            }

            public void Dispose()
            {
                _guard.Dispose();
            }
        }

        public ref struct Writer
        {
            MBInternal<T> _mb;
            readonly TestThreadSafety _guard;

            internal Writer(MBInternal<T> mb, Sentinel sentinel)
            {
                _mb = mb;
                _guard = sentinel.TestThreadSafety();
            }

            public int capacity => _mb.capacity;

            public ref T this[uint index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _mb[index];
            }

            public ref T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _mb[index];
            }

            public void Dispose()
            {
                _guard.Dispose();
            }
        }
    }
}
