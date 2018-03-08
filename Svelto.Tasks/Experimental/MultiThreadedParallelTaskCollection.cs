//this was a good excercise, but with my current knowledge, I say that heavy parallelism
//is useless for generic game features.
using System;
using System.Collections;
using System.Threading;
using Svelto.Utilities;

namespace Svelto.Tasks
{
    /// <summary>
    /// a ParallelTaskCollection ran by MultiThreadRunner will run the tasks in a single thread
    /// MultiThreadParallelTaskCollection enables parallel tasks to run on different threads
    /// </summary>
    public class  MultiThreadedParallelTaskCollection  : IEnumerator
    {
        public event Action onComplete;

        static readonly int MAX_CONCURRENT_TASKS = Environment.ProcessorCount;

        public void Reset()
        {
            for (int i = 0; i < _parallelTasks.Length; i++)
                _parallelTasks[i].Reset();
        }

        public object Current { get { return null; } }
        public bool isRunning { private set; get; }

        public MultiThreadedParallelTaskCollection()
        {
            uint numberOfThreads = (uint) Math.Max(MAX_CONCURRENT_TASKS, 4);

            InitializeThreadsAndData(numberOfThreads, true);
        }

        public MultiThreadedParallelTaskCollection(uint numberOfThreads, bool relaxed = true)
        {
            InitializeThreadsAndData(numberOfThreads, relaxed);
        }

        void InitializeThreadsAndData(uint numberOfThreads, bool relaxed)
        {
            _runners       = new MultiThreadRunner[numberOfThreads];
            _taskRoutines  = new ITaskRoutine[numberOfThreads];
            _parallelTasks = new ParallelTaskCollection[numberOfThreads];

            //prepare a single multithread runner for each group of fiber like task collections
            //number of threads can be less than the number of tasks to run
            for (int i = 0; i < numberOfThreads; i++)
                _runners[i] = new MultiThreadRunner("MultiThreadedParallelTask #".FastConcat(i), relaxed);

            /*Action*/ _ptcOnOnComplete = DecrementConcurrentOperationsCounter;
            Func<Exception, bool> ptcOnOnException = (e) =>
                                                     {
                                                         DecrementConcurrentOperationsCounter();
                                                         return false;
                                                     };
            _ptcOnOnException =   (e) => DecrementConcurrentOperationsCounter(); 

            //prepare the fiber-like paralleltasks
            for (int i = 0; i < numberOfThreads; i++)
            {
                var ptask = TaskRunner.Instance.AllocateNewTaskRoutine();
                var ptc   = new ParallelTaskCollection("ParallelTaskCollection #".FastConcat(i));

                ptc.onComplete  += _ptcOnOnComplete;
                ptc.onException += ptcOnOnException;

                ptask.SetEnumerator(ptc).SetScheduler(_runners[i]);

                _parallelTasks[i] = ptc;
                _taskRoutines[i]  = ptask;
            }
        }

        bool RunMultiThreadParallelTasks()
        {
            if (_taskRoutines == null)
                throw new MultiThreadedParallelTaskCollectionException("can't run a MultiThreadedParallelTaskCollection once killed");
            
            if (isRunning == false)
            {
                _counter = _numberOfConcurrentOperationsToRun;
                ThreadUtility.MemoryBarrier();
                //start them
                for (int i = 0; i < _numberOfConcurrentOperationsToRun; i++)
                    _taskRoutines[i].ThreadSafeStart(_ptcOnOnException, _ptcOnOnComplete);
            }
            
            //wait for completition, I am not using signaling as this Collection could be yielded by a main thread runner
            ThreadUtility.MemoryBarrier();
            isRunning = _counter > 0; 

            return isRunning;
        }
       
        public void Add(IEnumerator enumerator)
        {
            if (isRunning == true)
                throw new MultiThreadedParallelTaskCollectionException("can't add enumerators on a started MultiThreadedParallelTaskCollection");

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

            return false;
        }

        public void Stop()
        {
            for (int i = 0; i < _taskRoutines.Length; i++)
                _taskRoutines[i].Stop();

            while (_counter > 0) 
                ThreadUtility.Yield();
            
            isRunning = false;
            
            ThreadUtility.MemoryBarrier();
        }
        
        public void Complete()
        {
            RunMultiThreadParallelTasks();
            
            while (_counter > 0) 
                ThreadUtility.Yield();
            
            if (onComplete != null)
                onComplete();
            
            isRunning = false;
            
            ThreadUtility.MemoryBarrier();
        }

        public void ClearAndKill()
        {
            _runningThreads = _runners.Length;

            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Kill(DecrementRunningThread);

            _numberOfTasksAdded = 0;
            
            while (_runningThreads > 0) ThreadUtility.Yield();

            _taskRoutines = null;
            _parallelTasks = null;
            _runners = null;
            onComplete = null;
        }

        void DecrementRunningThread()
        {
            Interlocked.Decrement(ref _runningThreads);
        }

        void DecrementConcurrentOperationsCounter()
        {
            Interlocked.Decrement(ref _counter);
        }
        
        MultiThreadRunner[]      _runners;
        ParallelTaskCollection[] _parallelTasks;
        ITaskRoutine[]           _taskRoutines;
        
        int                           _numberOfTasksAdded;
        int                           _numberOfConcurrentOperationsToRun;
        int                           _counter;
        int                           _runningThreads;
        Action                        _ptcOnOnComplete;
        Action<PausableTaskException> _ptcOnOnException;
    }

    public class MultiThreadedParallelTaskCollectionException : Exception
    {
        public MultiThreadedParallelTaskCollectionException(string canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection):base(canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection)
        {}
    }
}
