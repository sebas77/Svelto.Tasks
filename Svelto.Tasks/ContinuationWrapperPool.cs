using Svelto.DataStructures;
using System;

namespace Svelto.Tasks.Internal
{
    /// <summary>
    /// In this interim version of Svelto.Tasks between 1.0 and 2.0, I made a mistake
    /// in the design of the continuation wrapper. Svelto.Tasks 2.0 will also use a static, thread safe
    /// pool of Continuation Wrapper, but in a more efficient way.
    /// </summary>
    static class ContinuationWrapperPool
    {
        internal static ContinuationWrapper Pull()
        {
            ContinuationWrapper task;

            if (_pool.Dequeue(out task))
            {
                GC.ReRegisterForFinalize(task);

                return task;
            }

            return Create();
        }

        internal static void Push(ContinuationWrapper task)
        {
            GC.SuppressFinalize(task); //will be register again once pulled from the pool
            _pool.Enqueue(task);
        }

        static ContinuationWrapper Create()
        {
            return new ContinuationWrapper(true);
        }

        static readonly LockFreeQueue<ContinuationWrapper> _pool = new LockFreeQueue<ContinuationWrapper>();
    }
}
    