using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    sealed class PausableTaskPool
    {
        public PooledPausableTask RetrieveTaskFromPool()
        {
            PooledPausableTask task;

            if (_pool.Dequeue(out task))
                return task;

            return CreateEmptyTask();
        }

        public void PushTaskBack(PooledPausableTask task)
        {
            _pool.Enqueue(task);
        }

        PooledPausableTask CreateEmptyTask()
        {
            return new PooledPausableTask(this);
        }

        LockFreeQueue<PooledPausableTask> _pool = new LockFreeQueue<PooledPausableTask>();
    }
}
