using System;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures
{
    public class ThreadSafeFasterList<T> 
    {
        public ThreadSafeFasterList(FasterList<T> list)
        {
            if (list == null) throw new ArgumentException("invalid list");
            _list = list;
            _lockQ = ReaderWriterLockSlimEx.Create();
        }

        public ThreadSafeFasterList()
        {
            _list  = new FasterList<T>();
            _lockQ = ReaderWriterLockSlimEx.Create();
        }

        public int count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                _lockQ.EnterReadLock();
                try
                {
                    return _list.count;
                }
                finally
                {
                    _lockQ.ExitReadLock();
                }
            }
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                _lockQ.EnterWriteLock();
                try
                {
                    return ref _list[index];
                }
                finally
                {
                    _lockQ.ExitWriteLock();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FasterListEnumerator<T> GetEnumerator()
        {
            return GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _list.Add(item);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(uint location, T item)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _list.AddAt(location, item);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _lockQ.EnterWriteLock();
            try
            {
                _list.Clear();
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(uint index, T item)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _list.InsertAt(index, item);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(uint index)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _list.RemoveAt(index);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnorderedRemoveAt(uint index)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _list.UnorderedRemoveAt(index);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ToArrayFast(out int count)
        {
            _lockQ.EnterReadLock();
            try
            {
                return _list.ToArrayFast(out count);
            }
            finally
            {
                _lockQ.ExitReadLock();
            }
        }

        readonly FasterList<T> _list;

        readonly ReaderWriterLockSlimEx _lockQ;
    }
}