using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    interface IPausableTaskPool
    {
        ITaskRoutine RetrieveTaskFromPool();
        void PushTaskBack(PausableTask task);
    }

    class PausableTaskPool : IPausableTaskPool
    {
        public ITaskRoutine RetrieveTaskFromPool()
        {
            if (_pool.Count > 0) return _pool.Pop();

            return CreateEmptyTask();
        }

        public void PushTaskBack(PausableTask task)
        {
            _pool.Push(task);
        }

        ITaskRoutine CreateEmptyTask()
        {
            return new PausableTask(this);
        }

        Stack<PausableTask> _pool = new Stack<PausableTask>();
    }
#if _DEPRECATED
    class PausableTaskPoolThreadSafe : IPausableTaskPool
    {
        public ITaskRoutine RetrieveTaskFromPool()
        {
            PausableTask task;

            if (_pool.TryDequeue(out task))
            {
                task.Reset();

                return task;
            }

            return CreateEmptyTask();
        }

        public void PushTaskBack(PausableTask task)
        {
            _pool.Enqueue(task);
        }

        ITaskRoutine CreateEmptyTask()
        {
            return new PausableTask(this);
        }

        ThreadSafeQueue<PausableTask> _pool = new ThreadSafeQueue<PausableTask>();
    }
#endif    
}
