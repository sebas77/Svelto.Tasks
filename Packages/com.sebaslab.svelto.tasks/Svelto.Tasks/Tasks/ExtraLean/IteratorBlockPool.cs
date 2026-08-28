using System;
using System.Collections;
using Svelto.DataStructures;

namespace Svelto.Tasks.ExtraLean
{
    public class PooledIteratorBlock<T>:IEnumerator where T : class, new()
    {
        IEnumerator iteratorBlock;
        T          data;
        IteratorBlockPool<T> pool;

        public PooledIteratorBlock(IEnumerator iEnumerator, T data, IteratorBlockPool<T> pool)
        {
            iteratorBlock = iEnumerator;
            this.pool = pool;
            this.data = data;
        }

        public bool MoveNext()
        {
            var canMove = iteratorBlock.MoveNext();
            //Only a break yield is a safe reusable boundary, and this plain-IEnumerator wrapper has no
            //Dispose() to defer the pool return to, so the return must stay here. A machine that ended
            //naturally (canMove == false / yield break) is dead and must NOT be pooled: it is simply
            //abandoned and left to the GC, and the next Get() allocates a replacement block.
            if (canMove && iteratorBlock.Current is TaskContract.Break taskContractBreak && taskContractBreak.AnyBreak)
            {
                pool.Return(data, this);
                return false;
            }

            return canMove;
        }
        public override string ToString()
        {
            return pool.name;
        }
        public void Reset() { }
        public object Current => iteratorBlock.Current;
    }

    /// <summary>
    /// Pool the iterator blocks, so that we can reuse them without having to allocate new ones every time.
    /// Iterators can be pooled thanks to the following pattern:
    ///   while(true) infinite loop, the state machine never ends
    ///   {
    ///       yield return TaskContract.Break.It; //signals the end of the iteration, but the state machine
    ///                                           //is not ended, so it can be reused
    ///   }
    ///
    /// Get and Return are thread safe. A block and its data remain exclusively owned by the caller from Get
    /// until they are returned, so callers must not use the same borrowed block concurrently.
    /// </summary>
    public class IteratorBlockPool<T> where T : class, new()
    {
        readonly ThreadSafeStack<(T data, PooledIteratorBlock<T> pooledIteratorBlock)> _pool = new ThreadSafeStack<(T data, PooledIteratorBlock<T> pooledIteratorBlock)>();
        readonly Func<T, IEnumerator> _iteratorBlock;
        internal readonly string name;

        public IteratorBlockPool(Func<T, IEnumerator> iteratorBlock, string profilingName, int initialPoolSize = 0)
        {
            _iteratorBlock = iteratorBlock;
            name = profilingName;

            for (int i = 0; i < initialPoolSize; i++)
            {
                var data = new T();
                _pool.Push((data, new PooledIteratorBlock<T>(_iteratorBlock(data), data, this)));
            }
        }

        public (T data, PooledIteratorBlock<T> pooledIteratorBlock) Get()
        {
            if (_pool.TryPop(out var item))
                return item;

            var data = new T();
            return (data, new PooledIteratorBlock<T>(_iteratorBlock(data), data, this));
        }

        public void Return(T data, PooledIteratorBlock<T> pooledIteratorBlock)
        {
            _pool.Push((data, pooledIteratorBlock));
        }

        public void Dispose()
        {
            while (_pool.TryPop(out _)) { }
        }
    }
}
