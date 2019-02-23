using System;
using Svelto.DataStructures;
using Svelto.Tasks.Enumerators;

namespace Svelto.Tasks.Internal
{
    static class ContinuationPool
    {
        static ContinuationPool()
        {
            for (int i = 0; i < 1000; i++) _pool.Enqueue(new ContinuationEnumeratorInternal());
        }
        
        public static ContinuationEnumeratorInternal RetrieveFromPool()
        {
            ContinuationEnumeratorInternal task;

            if (_pool.Dequeue(out task))
            {
                GC.ReRegisterForFinalize(task);

                return task;
            }

            return CreateEmpty();
        }

        public static void PushBack(ContinuationEnumeratorInternal task)
        {
            GC.SuppressFinalize(task); //will be register again once pulled from the pool
            
            _pool.Enqueue(task);
        }

        static ContinuationEnumeratorInternal CreateEmpty() 
        {
            return new ContinuationEnumeratorInternal();
        }

        static readonly LockFreeQueue<ContinuationEnumeratorInternal> _pool =
            new LockFreeQueue<ContinuationEnumeratorInternal>();
    }
}
