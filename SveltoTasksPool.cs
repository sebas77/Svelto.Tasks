using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    sealed class SveltoTasksPool
    {
        public PooledSveltoTask RetrieveTaskFromPool()
        {
            PooledSveltoTask task;

            if (_pool.TryDequeue(out task))
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

        readonly ThreadSafeQueue<PooledSveltoTask> _pool = new ThreadSafeQueue<PooledSveltoTask>();
    }
}
