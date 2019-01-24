using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    sealed class SveltoTasksPool
    {
        public PooledSveltoTask RetrieveTaskFromPool()
        {
            PooledSveltoTask task;

            if (_pool.Dequeue(out task))
                return task;

            return CreateEmptyTask();
        }

        public void PushTaskBack(PooledSveltoTask task)
        {
            _pool.Enqueue(task);
        }

        PooledSveltoTask CreateEmptyTask()
        {
            return new PooledSveltoTask(this);
        }

        readonly LockFreeQueue<PooledSveltoTask> _pool = new LockFreeQueue<PooledSveltoTask>();
    }
}
