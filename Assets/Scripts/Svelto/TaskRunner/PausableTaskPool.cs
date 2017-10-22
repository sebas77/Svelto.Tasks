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

            if (_pool.Dequeue(out task))
            {
                task.Reset();

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

        LockFreeQueue<PausableTask> _pool = new LockFreeQueue<PausableTask>();
    }
}
