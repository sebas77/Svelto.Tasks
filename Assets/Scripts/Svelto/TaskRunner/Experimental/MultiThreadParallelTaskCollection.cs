//this was a good excercise, but with my current knowledge, I say that heavy parallelism
//is useless for generic game features.
using System;
using System.Collections;
using Svelto.DataStructures;

namespace Svelto.Tasks
{
    /// <summary>
    /// a ParallelTaskCollection ran by MultiThreadRunner will run the tasks in a single thread
    /// MultiThreadParallelTaskCollection enables parallel tasks to run on different threads
    /// </summary>
    public class MultiThreadParallelTaskCollection : IEnumerator
    {
        public event Action onComplete;

        const int MAX_CONCURRENT_TASKS = 1024;

        public MultiThreadParallelTaskCollection(uint numberOfThreads = MAX_CONCURRENT_TASKS)
        {
            _runners = new MultiThreadRunner[numberOfThreads];
            _parallelTasks = new ParallelTaskCollection[numberOfThreads];

            for (int i = 0; i < numberOfThreads; i++)
            {
                _parallelTasks[i] = new ParallelTaskCollection();
                _parallelTasks[i].onComplete += DecrementConcurrentOperationsCounter;
            }

            for (int i = 0; i < numberOfThreads; i++) _runners[i] = new MultiThreadRunner();

            _enumeratorCopy = new FasterList<IEnumerator>();
        }

        bool RunMultiThreadParallelTasks()
        {
            if (isRunning == false)
            {
                int concurrentOperations = _enumeratorCopy.Count;
                for (int i = 0; i < concurrentOperations; i++)
                {
                    var yieldIT = _enumeratorCopy[i];

                    ParallelTaskCollection parallelTaskCollection = _parallelTasks[i % _parallelTasks.Length];
                    parallelTaskCollection.Add(yieldIT);
                }

                var numberOfConcurrentOperationsToRun = _counter = Math.Min(_parallelTasks.Length, concurrentOperations);

                for (int i = 0; i < numberOfConcurrentOperationsToRun; i++)
                    _parallelTasks[i].ThreadSafeRunOnSchedule(_runners[i]);
            }
#if NET_4_6
            System.Threading.Interlocked.MemoryBarrier();
#else
            System.Threading.Thread.MemoryBarrier();
#endif
            isRunning = _counter > 0;

            return isRunning;
        }

        public void Add(IEnumerator enumerator)
        {
            _enumeratorCopy.Add(enumerator);
        }

        void DecrementConcurrentOperationsCounter()
        {
            System.Threading.Interlocked.Decrement(ref _counter);
        }   

        public bool MoveNext()
        {
            if (RunMultiThreadParallelTasks()) return true;

            if (onComplete != null)
                onComplete();

            return false;
        }

        public void Reset()
        {
            _enumeratorCopy.Clear();
        }

        public object Current
        {
            get
            {
                return null;
            }
        }

        public bool              isRunning       { protected set; get; }

        MultiThreadRunner[]      _runners;
        int                      _counter;
        ParallelTaskCollection[] _parallelTasks;
        FasterList<IEnumerator> _enumeratorCopy;
    }
}
