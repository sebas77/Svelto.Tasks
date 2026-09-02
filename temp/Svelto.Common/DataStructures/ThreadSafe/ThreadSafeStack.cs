using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Svelto.DataStructures
{
    public class ThreadSafeStack<T>
    {
        public ThreadSafeValues GetValues => new ThreadSafeValues(_syncRoot, _stack);

        public void Push(in T value)
        {
            lock (_syncRoot)
            {
                _stack.Push(value);
            }
        }

        public bool TryPop(out T value)
        {
            lock (_syncRoot)
            {
                if (_stack.Count > 0)
                {
                    value = _stack.Pop();
                    return true;
                }

                value = default(T);
                return false;
            }
        }

        public uint count
        {
            get
            {
                lock (_syncRoot)
                {
                    return (uint) _stack.Count;
                }
            }
        }

        readonly Stack<T>   _stack;
        readonly object     _syncRoot;

        public ThreadSafeStack()
        {
            _stack    = new Stack<T>();
            _syncRoot = new object();
        }

        public struct ThreadSafeValues: IDisposable
        {
            object          _syncRoot;
            readonly Stack<T> _stack;

            public ThreadSafeValues(object syncRoot,
                Stack<T> stack):this()
            {
                Monitor.Enter(syncRoot);
                _syncRoot = syncRoot;
                _stack    = stack;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public IEnumerable<T> GetValues() => _stack;

            public void Dispose()
            {
                Monitor.Exit(_syncRoot);
            }
        }
    }
}