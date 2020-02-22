using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    public class ThreadSafeQueue<T>
    {
        public ThreadSafeQueue()
        {
            _queue = new Queue<T>(1);
        }

        public ThreadSafeQueue(int capacity)
        {
            _queue = new Queue<T>(capacity);
        }

        public void Enqueue(T item)
        {
            _lockQ.EnterWriteLock();
            try
            {
                _queue.Enqueue(item);
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        public T Dequeue()
        {
            _lockQ.EnterWriteLock();
            try
            {
                return _queue.Dequeue();
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        public void DequeueAllInto(FasterList<T> list)
        {
            int i = list.Count;
                
            _lockQ.EnterWriteLock();
            list.ExpandBy((uint) _queue.Count);
            var array = list.ToArrayFast();

            try
            {
                while (_queue.Count > 0)
                    array[i++] = _queue.Dequeue();
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        public void DequeueInto(FasterList<T> list, int count)
        {
            _lockQ.EnterWriteLock();
            try
            {
                int originalSize = _queue.Count;
                while (_queue.Count > 0 && originalSize - _queue.Count < count)
                    list.Add(_queue.Dequeue());
            }   
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        public T Peek()
        {
            T item = default(T);
            
            _lockQ.EnterReadLock();
            try
            {
                if (_queue.Count > 0)
                    item = _queue.Peek();
            }
            finally
            {
                _lockQ.ExitReadLock();
            }
            
            return item;
        }

        public void Clear()
        {
            _lockQ.EnterWriteLock();
            try
            {
                _queue.Clear();
            }
            finally
            {
                _lockQ.ExitWriteLock();
            }
        }

        public bool TryDequeue(out T item)
        {
            _lockQ.EnterUpgradableReadLock();
            try
            {
                if (_queue.Count > 0)
                {
                    _lockQ.EnterWriteLock();
                    try
                    {
                        item = _queue.Dequeue();
                    }
                    finally
                    {
                        _lockQ.ExitWriteLock();
                    }
                    return true;
                }

                item = default(T);
                
                return false;
            }
            finally
            {
                _lockQ.ExitUpgradableReadLock();
            }
        }

        public int Count
        {
            get
            {
                _lockQ.EnterReadLock();
                try
                {
                    return _queue.Count;
                }
                finally
                {
                    _lockQ.ExitReadLock();
                }
            }
        }
        
        readonly Queue<T>             _queue;
        readonly ReaderWriterLockSlimEx _lockQ = ReaderWriterLockSlimEx.Create();
    }
}
