//this was a good excercise, but with my current knowledge, I say that heavy parallelism
//is useless for generic game features.
using System;
using System.Collections;

namespace Svelto.Tasks
{
    /// <summary>
    /// a ParallelTaskCollection ran by MultiThreadRunner will run the tasks in a single thread
    /// MultiThreadParallelTaskCollection enables parallel tasks to run on different threads
    /// </summary>
    public class MultiThreadedParallelTaskCollection : IEnumerator
    {
        public event Action onComplete;

        const int MAX_CONCURRENT_TASKS = 1024;

        public object Current { get { return null; } }
        public bool isRunning { protected set; get; }

        public MultiThreadedParallelTaskCollection(int numberOfThreads = MAX_CONCURRENT_TASKS, bool relaxed = false)
        {
            _runners = new MultiThreadRunner[numberOfThreads];
            _taskRoutines = new ITaskRoutine[numberOfThreads];
            _parallelTasks = new ParallelTaskCollection[numberOfThreads];

            //prepare a single multithread runner for each group of fiber like task collections
            //number of threads can be less than the number of tasks to run
            for (int i = 0; i < numberOfThreads; i++) _runners[i] = new MultiThreadRunner("MultiThreadedParallelTask:".FastConcat(i), relaxed);

            //prepare the fiber-like paralleltasks
            for (int i = 0; i < numberOfThreads; i++)
            {
                var ptask = TaskRunner.Instance.AllocateNewTaskRoutine();
                var ptc = new ParallelTaskCollection();
                ptc.onComplete += DecrementConcurrentOperationsCounter;

                ptask.SetEnumerator(ptc).SetScheduler(_runners[i]);

                _parallelTasks[i] = ptc;
                _taskRoutines[i] = ptask;
                //once they are all done, the process will be completed               
            }
        }

        bool RunMultiThreadParallelTasks()
        {
            if (_taskRoutines == null)
                throw new Exception("can't run a MultiThreadedParallelTaskCollection once killed");

            if (isRunning == false)
            {
                _counter = _numberOfConcurrentOperationsToRun;
                //start them
                for (int i = 0; i < _numberOfConcurrentOperationsToRun; i++)
                    _taskRoutines[i].ThreadSafeStart();
            }
            
            //wait for completition, I am not using signaling as this Collection could be yielded by a main thread runner
            isRunning = _counter > 0; 

            return isRunning;
        }

       
        public void Add(IEnumerator enumerator)
        {
            if (isRunning == true)
                throw new Exception("can't add enumerators on a started MultiThreadedParallelTaskCollection");

            ParallelTaskCollection parallelTaskCollection = _parallelTasks[_numberOfTasksAdded++ % _parallelTasks.Length];
            parallelTaskCollection.Add(enumerator);

            //decide how many threads to run
            _numberOfConcurrentOperationsToRun = Math.Min(_parallelTasks.Length, _numberOfTasksAdded);
        }

        public bool MoveNext()
        {
            if (RunMultiThreadParallelTasks()) return true;

            if (onComplete != null)
                onComplete();

            Reset();

            return false;
        }

        public void Reset()
        {}

        public void Clear()
        {
            _numberOfTasksAdded = 0;
            _counter = 0;
            for (int i = 0; i < _taskRoutines.Length; i++)
                _taskRoutines[i].Stop();
            var length = _parallelTasks.Length;
            for (int i = 0; i < length; i++)
                _parallelTasks[i].Clear();
        }

        public void ClearAndKill()
        {
            Clear();
            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Kill();
            _taskRoutines = null;
            _parallelTasks = null;
        }

        void DecrementConcurrentOperationsCounter()
        {
            System.Threading.Interlocked.Decrement(ref _counter);
        }   

        MultiThreadRunner[]         _runners;
        int                         _counter;
        ParallelTaskCollection[]    _parallelTasks;
        ITaskRoutine[]              _taskRoutines;
        int                         _numberOfTasksAdded;
        int                         _numberOfConcurrentOperationsToRun;
    }
}
