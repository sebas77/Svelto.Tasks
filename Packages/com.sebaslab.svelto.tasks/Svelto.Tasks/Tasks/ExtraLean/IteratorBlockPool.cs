using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

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
            if (canMove == false || iteratorBlock.Current is TaskContract.Break taskContractBreak && taskContractBreak.AnyBreak)
            {
                pool.Return(data, this);
                return false;
            }

            return true;
        }
        public override string ToString()
        {
            return pool.name;
        }
        public void Reset() { }
        public object Current => iteratorBlock.Current;
    }
    
    public class IteratorBlockPool<T> where T : class, new()
    {
        readonly ConcurrentStack<(T data, PooledIteratorBlock<T> pooledIteratorBlock)> _pool = new ConcurrentStack<(T data, PooledIteratorBlock<T> pooledIteratorBlock)>();
        readonly Func<T, IEnumerator> _iteratorBlock;
        internal readonly string name;

        public IteratorBlockPool(Func<T, IEnumerator> iteratorBlock, string profilingName)
        {
            _iteratorBlock = iteratorBlock;
            name = profilingName;
        }

        public (T data, PooledIteratorBlock<T> pooledIteratorBlock) Get()
        {
            if (_pool.Count == 0)
            {
                var data = new T();

                Return(data, new PooledIteratorBlock<T>(_iteratorBlock(data), data, this));
            }

            return _pool.TryPop(out var result) ? result : default;
        }

        public void Return(T data, PooledIteratorBlock<T> pooledIteratorBlock)
        {
            _pool.Push((data, pooledIteratorBlock));
        }
        
        public void Dispose()
        {
            _pool.Clear();
        }
    }
}