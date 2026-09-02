using System;
using System.Collections.Generic;
using Svelto.DataStructures;

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
        //Set only when the underlying iterator reached its deliberate reusable boundary (Break.It/Break.AndStop).
        //Dispose() uses it to decide between releasing the block back to the pool and permanently discarding it.
        bool returnToPool;

        public PooledIteratorBlock(IEnumerator<TaskContract> iEnumerator, T data, IteratorBlockPool<T> pool)
        {
            iteratorBlock = iEnumerator;
            this.pool = pool;
            this.data = data;
        }

        public bool MoveNext()
        {
            var canMove = iteratorBlock.MoveNext();

            //A break yield is the only safe reusable boundary: the state machine is suspended exactly after
            //it, so the next borrow resumes at the top of the enclosing loop. Just flag it here; the actual
            //pool return happens in Dispose(), once the runner has fully released this execution. Returning
            //the block to the pool inside MoveNext (the old behavior) could hand out a block the runner still
            //holds, and would pool machines abandoned mid-cycle.
            if (canMove && iteratorBlock.Current is TaskContract taskContract && taskContract.breakMode != null
             && taskContract.breakMode.AnyBreak)
            {
                returnToPool = true;
                return false;
            }

            //A machine that ended naturally (MoveNext == false / yield break) is dead and must never be
            //pooled; it will be disposed by Dispose() and a replacement block is allocated by the next Get().
            return canMove;
        }

        public override string ToString() => pool.name;

        //Called by the runner when it abandons the task (completion, break, Stop, fault, Flush/Dispose) and by
        //IteratorBlockPool.Dispose() for idle blocks. Only a break-flagged execution goes back to the pool;
        //any other case permanently disposes the underlying iterator and the block is left to the GC.
        public void Dispose()
        {
            if (returnToPool)
            {
                returnToPool = false; //reset before pooling so the next cycle starts clean
                pool.Return(data, this);
            }
            else
            {
                iteratorBlock?.Dispose();
            }
        }

        public void Reset() => throw new NotImplementedException();
        public object Current => throw new NotImplementedException();

        TaskContract IEnumerator<TaskContract>.Current => iteratorBlock.Current;
    }

    /// <summary>
    /// The idea behind this class is to pool the iterator blocks, so that we can reuse them without having
    /// to allocate new ones every time. Iterators can be pooled thanks to the use of the following pattern:
    ///   while(true) infinite loop, the state machine never ends.
    ///   {
    ///       yield return TaskContract.Break.It; //signals the end of the iteration, but the state machine
    ///                                           //is not ended, so it can be reused
    ///   }
    ///
    /// Get and Return are thread safe. A block and its data remain exclusively owned by the caller from Get
    /// until they are returned, so callers must not use the same borrowed block concurrently.
    /// </summary>
    public class IteratorBlockPool<P> where P : class, new()
    {
        readonly ThreadSafeStack<(P data, PooledIteratorBlock<P> pooledIteratorBlock)> _pool = new ThreadSafeStack<(P data, PooledIteratorBlock<P> pooledIteratorBlock)>();
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
        public int count => (int)_pool.count;

        public (P data, PooledIteratorBlock<P> pooledIteratorBlock) Get()
        {
            if (_pool.TryPop(out var item))
                return item;

            var data = new P();
            return (data, new PooledIteratorBlock<P>(_iteratorBlock(data), data, this));
        }

        public void Return(P data, PooledIteratorBlock<P> pooledIteratorBlock)
        {
            _pool.Push((data, pooledIteratorBlock));
        }

        public void Dispose()
        {
            //Idle blocks are not break-flagged, so their Dispose() permanently disposes the underlying
            //iterators. Borrowed blocks are not drained: stop the runners first (see the pool contract).
            while (_pool.TryPop(out var item))
            {
                item.pooledIteratorBlock.Dispose();
            }
        }
    }
}
