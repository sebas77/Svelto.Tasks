using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    static class ContinuationWrapperPool
    {
        static ContinuationWrapperPool()
        {
            for (int i = 0; i < 100000; i++) _pool.Enqueue(new ContinuationWrapper());
        }
        
        public static ContinuationWrapper RetrieveFromPool()
        {
            ContinuationWrapper task;

            if (_pool.Dequeue(out task))
                return task;

            return CreateEmpty();
        }

        public static void PushBack(ContinuationWrapper task)
        {
            _pool.Enqueue(task);
        }

        static ContinuationWrapper CreateEmpty()
        {
            return new ContinuationWrapper();
        }

        static readonly LockFreeQueue<ContinuationWrapper> _pool = new LockFreeQueue<ContinuationWrapper>();
    }
}
