using System.Collections.Concurrent;
using Svelto.Tasks.Enumerators;

namespace Svelto.Tasks.Internal
{
    public static class ContinuationEnumeratorPool
    {
        public static ContinuationEnumerator RetrieveFromPool()
        {
            if (_pool.TryDequeue(out var task))
            {
                return task;
            }

            return new ContinuationEnumerator();
        }

        public static void PushBack(ContinuationEnumerator task)
        {
            _pool.Enqueue(task);
        }

#pragma warning disable CLG001
        static readonly ConcurrentQueue<ContinuationEnumerator> _pool = new ConcurrentQueue<ContinuationEnumerator>();
#pragma warning restore CLG001
    }
}