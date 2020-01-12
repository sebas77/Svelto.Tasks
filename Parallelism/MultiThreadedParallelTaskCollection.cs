using System;
using System.Collections;
using System.Threading;
using Svelto.Tasks.Parallelism.Internal;
using Svelto.Utilities;

namespace Svelto.Tasks.Parallelism
{
    /// <summary>
    /// a ParallelTaskCollection ran by MultiThreadRunner will run the tasks in a single thread
    /// MultiThreadParallelTaskCollection enables parallel tasks to run on different threads
    ///
    /// Todo for svelto tasks 2: remove the ParallelTaskCollection dependency
    /// </summary>
    public class MultiThreadedParallelTaskCollection  : IEnumerator, IDisposable
    {
        public event Action onComplete;

        public object Current { get { return null; } }
        public bool isRunning { private set; get; }

        /// <summary>
        ///  
        /// </summary>
        /// <param name="numberOfThreads"></param>
        /// <param name="tightTasks">
        /// if several cache friendly and optimized tasks run in parallel, using tightTasks may improve parallelism
        /// as gives the chance to other threads to run.
        /// </param>
        public MultiThreadedParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
        {
            DBC.Tasks.Check.Require(numberOfThreads > 1, "doesn't make much sense to use this with just 1 thread");
            
            _name = name;
            
            InitializeThreadsAndData(numberOfThreads, tightTasks);
        }

        /// <summary>
        /// Add can be called by another thread, so if the collection is already running
        /// I can't allow adding more tasks.
        /// </summary>
        /// <param name="enumerator"></param>
        /// <exception cref="MultiThreadedParallelTaskCollectionException"></exception>
        public void Add(IEnumerator enumerator)
        {
            if (isRunning == true)
                throw new MultiThreadedParallelTaskCollectionException("can't add tasks on a started MultiThreadedParallelTaskCollection");

            ParallelTaskCollection parallelTaskCollection = _parallelTasks[_numberOfTasksAdded++ % _parallelTasks.Length];
            parallelTaskCollection.Add(enumerator);

            _numberOfConcurrentOperationsToRun = Math.Min(_parallelTasks.Length, _numberOfTasksAdded);
        }
        
        public void Add<T>(ref T job, int iterations) where T:struct, ISveltoJob
        {
            if (isRunning == true)
                throw new MultiThreadedParallelTaskCollectionException("can't add tasks on a started MultiThreadedParallelTaskCollection");

            var runnersLength = _runners.Length;
            int itemsPerThread = (int) Math.Floor((double)iterations / runnersLength);
            int reminder = iterations % runnersLength;

            for (int i = 0; i < runnersLength; i++)
                Add(new ParallelRunEnumerator<T>(ref job, itemsPerThread * i, itemsPerThread));
            
            if (reminder > 0)
                Add(new ParallelRunEnumerator<T>(ref job, itemsPerThread * runnersLength, reminder));
        }

        public void Clear()
        {
            isRunning = false;
            foreach (var parallelTask in _parallelTasks) parallelTask.Clear();

            _numberOfTasksAdded = 0;
            _numberOfConcurrentOperationsToRun = 0;
        }

        public bool MoveNext()
        {
            if (ThreadUtility.VolatileRead(ref _isDisposed)) return false;
            
            if (RunMultiThreadParallelTasks()) return true;
            
            if (onComplete != null)
                onComplete();

            isRunning = false;

            return false;
        }
        
        public void Reset()
        {
            DBC.Tasks.Check.Require(ThreadUtility.VolatileRead(ref _isDisposed) == false, 
                                    "trying to reset a disposed MultiThreadedParallelTaskCollection");
            
            for (int i = 0; i < _parallelTasks.Length; i++)
                _parallelTasks[i].Reset();
        }

        public void Stop()
        {
            _stoppingThreads = _taskRoutines.Length;
            
            for (int i = 0; i < _runners.Length; i++)
                _runners[i].StopAllCoroutines();

            while (_stoppingThreads > 0) 
                ThreadUtility.TakeItEasy();
            
            isRunning = false;
            
            ThreadUtility.MemoryBarrier();
        }
        
        public void Dispose()
        {
            if (ThreadUtility.VolatileRead(ref _isDisposed) == true) return;
            
            ThreadUtility.VolatileWrite(ref _isDisposed, true);
            _disposingThreads = _runners.Length;

            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Kill(_decrementRunningThread);

            while (_disposingThreads > 0) 
                ThreadUtility.TakeItEasy();
            
            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Dispose();

            _runners            = null;
            _parallelTasks      = null;
            onComplete          = null;
            _numberOfTasksAdded = 0;
            isRunning           = false;
            
            ThreadUtility.MemoryBarrier();
            
            GC.SuppressFinalize(this);
        }

        ~MultiThreadedParallelTaskCollection()
        {
            Dispose();
        }

        public override string ToString()
        {
            return _name;
        }
        
        void InitializeThreadsAndData(uint numberOfThreads, bool tightTasks)
        {
            _decrementRunningThread = DecrementRunningThread;
            
            _runners       = new MultiThreadRunner[numberOfThreads];
            _taskRoutines  = new ITaskRoutine<IEnumerator>[numberOfThreads];
            _parallelTasks = new ParallelTaskCollection[numberOfThreads];

            //prepare a single multithread runner for each group of fiber like task collections
            //number of threads can be less than the number of tasks to run
            for (int i = 0; i < numberOfThreads; i++)
                _runners[i] = new MultiThreadRunner("MultiThreadedParallelRunner ".FastConcat(_name," #").FastConcat(i), 
                                                    false, tightTasks);

            Func<Exception, bool> ptcOnOnException = (e) =>  {
                                                                    DecrementConcurrentOperationsCounter();
                                                                    return false;
                                                             };

            Action ptcOnOnComplete = DecrementConcurrentOperationsCounter;
            //prepare the fiber-like paralleltasks
            for (int i = 0; i < numberOfThreads; i++)
            {
                var ptask = TaskRunner.Instance.AllocateNewTaskRoutine(_runners[i]);
                var ptc   = new ParallelTaskCollection("MultiThreaded ParallelTaskCollection ".FastConcat(_name," #").FastConcat(i));

                ptc.onComplete  += ptcOnOnComplete;
                ptc.onException += ptcOnOnException;

                ptask.SetEnumerator(ptc);

                _parallelTasks[i] = ptc;
                _taskRoutines[i]  = ptask;
            }
        }

        bool RunMultiThreadParallelTasks()
        {
            if (_taskRoutines == null)
                throw new MultiThreadedParallelTaskCollectionException("can't run a MultiThreadedParallelTaskCollection " +
                                                                       "once killed");
            
            if (isRunning == false)
            {
                _counter = _numberOfConcurrentOperationsToRun;
                ThreadUtility.MemoryBarrier();
                
                Action ptcOnOnComplete = DecrementStoppingThread;
                //start them
                for (int i = 0; i < _numberOfConcurrentOperationsToRun; i++)
                    _taskRoutines[i].Start(onStop: ptcOnOnComplete);
            }
            
            //wait for completition, I am not using signaling as this Collection could be yielded by a main thread runner
            ThreadUtility.MemoryBarrier();
            isRunning = _counter > 0; 

            return isRunning;
        }

        void DecrementRunningThread()
        {
            Interlocked.Decrement(ref _disposingThreads);
        }
        
        void DecrementStoppingThread()
        {
            Interlocked.Decrement(ref _stoppingThreads);
        }

        void DecrementConcurrentOperationsCounter()
        {
            Interlocked.Decrement(ref _counter);
        }
        
        MultiThreadRunner[]         _runners;
        ParallelTaskCollection[]    _parallelTasks;
        ITaskRoutine<IEnumerator>[] _taskRoutines;
        
        int    _numberOfTasksAdded;
        int    _numberOfConcurrentOperationsToRun;
        int    _counter;
        int    _disposingThreads;
        int    _stoppingThreads;
        bool   _isDisposed;
        Action _decrementRunningThread;
        string _name;
    }

    public class MultiThreadedParallelTaskCollectionException : Exception
    {
        public MultiThreadedParallelTaskCollectionException(string canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection):base(canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection)
        {}
    }
}
