using System;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures
{
    /// <summary>
    /// Remember: concurrent dictionary allocates for each node add in the dictionary for each TValue that is not
    /// recognised as atomic (64bit max). This is the reason I keep this around. For all the atomic TValues
    /// Concurrent dictionary should be used
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public sealed class ThreadSafeDictionary<TKey, TValue> : IDisposable where TKey : struct, IEquatable<TKey>
    {
        public ThreadSafeDictionary(int size)
        {
            _dict = new FasterDictionary<TKey, TValue>((uint)size);
        }

        public ThreadSafeDictionary()
        {
            _dict = new FasterDictionary<TKey, TValue>();
        }

        public void Dispose()
        {
            _lockQ.EnterWriteLock();
            try
            {
                _dict.Dispose();
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        public int count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                _lockQ.EnterReadLock();
                try
                {
                    return _dict.count;
                }
                finally
                {
                    _lockQ.ExitReadLock();
                }
            }
        }

        public struct ThreadSafeValues: IDisposable
        {
            ReaderWriterLockSlimEx                  _lockQ;
            readonly FasterDictionary<TKey, TValue> _dic;

            public ThreadSafeValues(ReaderWriterLockSlimEx lockQ, FasterDictionary<TKey, TValue> dic) : this()
            {
                lockQ.EnterReadLock();
                _lockQ = lockQ;
                _dic   = dic;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public MB<TValue> GetValues(out uint count) => _dic.GetValues(out count);

            public void Dispose()
            {
                _lockQ.ExitReadLock();
            }
        }

        public ThreadSafeValues GetValues => new ThreadSafeValues(_lockQ, this._dict);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TKey key, in TValue value)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _dict.Add(key, in value);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(TKey key, in TValue value)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _dict.Set(key, in value);
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
                _dict.Clear();
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key)
        {
            _lockQ.EnterReadLock();
            try
            {
                return _dict.ContainsKey(key);
            }
            finally
            {
                _lockQ.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue result)
        {
            _lockQ.EnterReadLock();
            try
            {
                return _dict.TryGetValue(key, out result);
            }
            finally
            {
                _lockQ.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetOrAdd<W>(TKey key, Func<W> func) where W : class, TValue
        {
            _lockQ.EnterUpgradableReadLock();
            try
            {
                if (_dict.TryGetValue(key, out var ret))
                {
                    return ret;
                }

                _lockQ.EnterWriteLock();
                try
                {
                    TValue tValue = func();
                    _dict.Add(key, tValue);
                    return tValue;
                }
                finally
                {
                    _lockQ.ExitWriteLock();
                }
            }
            finally
            {
                _lockQ.ExitUpgradableReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetDirectValueByRef(uint index)
        {
            throw new NotSupportedException("this is too unsafe to use in a multithreaded scenario");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetValueByRef(TKey key)
        {
            throw new NotSupportedException("this is too unsafe to use in a multithreaded scenario");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(uint size)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _dict.EnsureCapacity(size);
            }
            finally
            {
                _lockQ.EnterWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncreaseCapacityBy(uint size)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _dict.IncreaseCapacityBy(size);
            }
            finally
            {
                _lockQ.EnterWriteLock();
            }
        }

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                _lockQ.EnterReadLock();
                try
                {
                    return _dict[key];
                }
                finally
                {
                    _lockQ.ExitReadLock();
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _lockQ.EnterUpgradableReadLock();
                try
                {
                    if (_dict.TryFindIndex(key, out var index))
                    {
                        _lockQ.EnterWriteLock();
                        try
                        {
                            _dict.GetDirectValueByRef(index) = value;
                            return;
                        }
                        finally
                        {
                            _lockQ.ExitWriteLock();
                        }
                    }

                    _lockQ.EnterWriteLock();
                    try
                    {
                        _dict.Add(key, value);
                    }
                    finally
                    {
                        _lockQ.ExitWriteLock();
                    }
                }
                finally
                {
                    _lockQ.ExitUpgradableReadLock();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TKey key)
        {
            _lockQ.EnterWriteLock();
            try
            {
                return _dict.Remove(key);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemove(TKey key)
        {
            _lockQ.EnterWriteLock();
            try
            {
                return _dict.Remove(key);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemove(TKey key, out TValue val)
        {
            _lockQ.EnterWriteLock();
            try
            {
                return _dict.Remove(key, out val);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Trim()
        {
            _lockQ.EnterWriteLock();
            try
            {
                _dict.Trim();
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindIndex(TKey key, out uint findIndex)
        {
            _lockQ.EnterReadLock();
            try
            {
                return _dict.TryFindIndex(key, out findIndex);
            }
            finally
            {
                _lockQ.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetIndex(TKey key)
        {
            _lockQ.EnterReadLock();
            try
            {
                return _dict.GetIndex(key);
            }
            finally
            {
                _lockQ.ExitReadLock();
            }
        }

        readonly FasterDictionary<TKey, TValue> _dict;
        readonly ReaderWriterLockSlimEx         _lockQ = ReaderWriterLockSlimEx.Create();
    }
}