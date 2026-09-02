using System;
using System.Collections.Concurrent;
using Svelto.Tasks.Enumerators;

namespace Svelto.Tasks.Internal
{
    static class ContinuationPool
    {
        const int PREALLOCATED_ENUMERATORS_NUMBER = 1000;

        static ContinuationPool()
        {
            for (int i = 0; i < PREALLOCATED_ENUMERATORS_NUMBER; i++) _pool.Enqueue(new ContinuationEnumeratorInternal());
        }
        
        public static ContinuationEnumeratorInternal RetrieveFromPool()
        {
            if (_pool.TryDequeue(out var task))
            {
                GC.ReRegisterForFinalize(task); //force them to return to the pool once they are not used anymore

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

#pragma warning disable CLG001
        static readonly ConcurrentQueue<ContinuationEnumeratorInternal> _pool =
#pragma warning restore CLG001
                new ConcurrentQueue<ContinuationEnumeratorInternal>();
    }
}
