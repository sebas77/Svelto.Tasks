using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures
{
    public class ThreadSafeStack<T>
    {
        public ThreadSafeValues GetValues => new ThreadSafeValues(_lockQ, _stack);

        public void Push(in T value)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _stack.Push(value);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        public bool TryPop(out T value)
        {
            _lockQ.EnterUpgradableReadLock();
            try
            {
                if (_stack.Count > 0)
                {
                    _lockQ.EnterWriteLock();
                    try
                    {
                        value = _stack.Pop();
                    }
                    finally
                    {
                        _lockQ.ExitWriteLock();
                    }
                    
                    return true;
                }

                value = default(T);
                
                return false;
            }
            finally
            {
                _lockQ.ExitUpgradableReadLock();
            }
        }
        
        public uint count
        {
            get
            {
                _lockQ.EnterReadLock();
                try
                {
                    return (uint) _stack.Count;
                }
                finally
                {
                    _lockQ.ExitReadLock();
                }
            }
        }

        readonly Stack<T>      _stack;
        ReaderWriterLockSlimEx _lockQ;

        public ThreadSafeStack()
        {
            _stack = new Stack<T>();
            _lockQ = ReaderWriterLockSlimEx.Create();
        }

        public struct ThreadSafeValues: IDisposable
        {
            ReaderWriterLockSlimEx _lockQ;
            readonly Stack<T>      _stack;

            public ThreadSafeValues(ReaderWriterLockSlimEx lockQ,
                Stack<T> stack):this()
            {
                lockQ.EnterReadLock();
                _lockQ = lockQ;
                _stack = stack;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public IEnumerable<T> GetValues() => _stack;

            public void Dispose()
            {
                _lockQ.ExitReadLock();
            }
        }
    }
}