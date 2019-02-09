using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    static class ContinuationWrapperPool
    {
        static ContinuationWrapperPool()
        {
            for (int i = 0; i < 1000; i++) _pool.Enqueue(new ContinuationEnumerator());
        }
        
        public static ContinuationEnumerator RetrieveFromPool()
        {
            ContinuationEnumerator task;

            if (_pool.Dequeue(out task))
                return task;

            return CreateEmpty();
        }

        public static void PushBack(ContinuationEnumerator task)
        {
            _pool.Enqueue(task);
        }

        static ContinuationEnumerator CreateEmpty()
        {
            return new ContinuationEnumerator();
        }

        static readonly LockFreeQueue<ContinuationEnumerator> _pool = new LockFreeQueue<ContinuationEnumerator>();
    }
}
