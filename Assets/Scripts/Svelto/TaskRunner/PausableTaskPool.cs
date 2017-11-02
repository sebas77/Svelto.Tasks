using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    sealed class PausableTaskPool
    {
        public PausableTask RetrieveTaskFromPool()
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
            task.CleanUp(); //let's avoid leakings

            _pool.Enqueue(task);
        }

        PausableTask CreateEmptyTask()
        {
            return new PausableTask(this);
        }

        LockFreeQueue<PausableTask> _pool = new LockFreeQueue<PausableTask>();
    }
}
