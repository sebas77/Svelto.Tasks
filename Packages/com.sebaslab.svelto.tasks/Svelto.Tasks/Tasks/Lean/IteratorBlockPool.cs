using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Svelto.Tasks.Lean
{
    //Wrap the iterator block in a pooled wrapper, so that we can reuse the same iterator block without having to allocate new ones every time.
    //Data is a class that can be used to store any data that the iterator block needs to run, so that we can reuse the same iterator block with different data.
    //for this reason Data must be a class, so that it's value can be changed without having to change the reference to the iterator block.
    public class PooledIteratorBlock<T>:IEnumerator<TaskContract> where T : class, new()
    {
        IEnumerator<TaskContract> iteratorBlock;
        T data;
        IteratorBlockPool<T> pool;

        public PooledIteratorBlock(IEnumerator<TaskContract> iEnumerator, T data, IteratorBlockPool<T> pool)
        { 
            iteratorBlock = iEnumerator;
            this.pool = pool;
            this.data = data;
        }

        public bool MoveNext()
        {
            var canMove = iteratorBlock.MoveNext();
            if (canMove == false || iteratorBlock.Current is TaskContract taskContract && taskContract.breakMode != null
             && taskContract.breakMode.AnyBreak)
            {
                pool.Return(data, this);
                return false;
            }

            return true;
        }
        
        public override string ToString() => pool.name;

        public void Dispose() => iteratorBlock?.Dispose();

        public void Reset() => throw new NotImplementedException();
        public object Current => throw new NotImplementedException();

        TaskContract IEnumerator<TaskContract>.Current => iteratorBlock.Current;
    }
    
    // The idea behind this class is to pool the iterator blocks, so that we can reuse them without having to allocate new ones every time.
    // Iterators can be pooled thanks to the use of the following patter:
    //  while (true) infinite loop, the state machine never ends.
//      {
    //    yield return TaskContract.Break.It; special yield that signals the end of the iteration, but the state machine is not ended, so it can be reused.
  //    }
    public class IteratorBlockPool<P> where P : class, new()
    {
        readonly ConcurrentStack<(P data, PooledIteratorBlock<P> pooledIteratorBlock)> _pool = new ConcurrentStack<(P data, PooledIteratorBlock<P> pooledIteratorBlock)>();
        readonly Func<P, IEnumerator<TaskContract> > _iteratorBlock;
        internal readonly string name;

        public IteratorBlockPool(Func<P, IEnumerator<TaskContract>> iteratorBlock, string profilingName)
        {
            _iteratorBlock = iteratorBlock;
            name = profilingName;
        }

        /// <summary>
        /// Number of iterator blocks currently sitting idle in the pool.
        /// </summary>
        public int count => _pool.Count;

        public (P data, PooledIteratorBlock<P> pooledIteratorBlock) Get()
        {
            if (_pool.Count == 0)
            {
                var data = new P();

                Return(data, new PooledIteratorBlock<P>(_iteratorBlock(data), data, this));
            }

            return _pool.TryPop(out var result) ? result : default;
        }

        public void Return(P data, PooledIteratorBlock<P> pooledIteratorBlock)
        {
            _pool.Push((data, pooledIteratorBlock));
        }

        public void Dispose()
        {
            while (_pool.Count > 0)
            {
                 _pool.TryPop(out var result);
                result.pooledIteratorBlock.Dispose();
            }
        }
    }
}