using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    interface IPausableTaskPool
    {
        ITaskRoutine RetrieveTaskFromPool();

        void PushTaskBack(PausableTask task);
    }

    class PausableTaskPool: IPausableTaskPool
    {
        public ITaskRoutine RetrieveTaskFromPool()
        {
            PausableTask task;

            if (_pool.TryDequeue(out task))
            {
                return task;
            }

            return CreateEmptyTask();
        }

        public void PushTaskBack(PausableTask task)
        {
            task.Reset();

            _pool.Enqueue(task);
        }

        ITaskRoutine CreateEmptyTask()
        {
            return new PausableTask(this);
        }

        ThreadSafeQueue<PausableTask> _pool = new ThreadSafeQueue<PausableTask>();
    }
}
