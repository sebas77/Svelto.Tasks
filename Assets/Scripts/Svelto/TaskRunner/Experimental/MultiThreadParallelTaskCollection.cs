#if _DEPRECATED
//this was a good excercise, but with my current knowledge, I say that heavy parallelism
//is useless for generic game features.
using System;
using System.Collections;
using System.Threading;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    /// <summary>
    /// a ParallelTaskCollection ran by MultiThreadRunner will run the tasks in a single thread
    /// MultiThreadParallelTaskCollection enables parallel tasks to run on different threads
    /// </summary>
    public class MultiThreadParallelTaskCollection : TaskCollection
    {
        const int MAX_CONCURRENT_TASK = 8;

        public event Action onComplete;
#if TO_IMPLEMENT_PROPERLY
        override public float progress { get { return _progress; } }
#endif
        public MultiThreadParallelTaskCollection(MultiThreadRunner runner) : base()
        {
            _maxConcurrentTasks = uint.MaxValue;
            _taskRoutinePool = new PausableTaskPoolThreadSafe();
            _runner = runner;

            ComputeMaxConcurrentTasks();
        }

        public MultiThreadParallelTaskCollection(MultiThreadRunner runner, uint maxConcurrentTasks) : this(runner)
        {
            _maxConcurrentTasks = Math.Min(MAX_CONCURRENT_TASK, maxConcurrentTasks);
        }

        void ComputeMaxConcurrentTasks()
        {
            if (_maxConcurrentTasks != uint.MaxValue)
                _maxConcurrentTasks = Math.Min(_maxConcurrentTasks, (uint)(registeredEnumerators.Count));
        }

        override public IEnumerator GetEnumerator()
        {
            _startingCount = registeredEnumerators.Count;

            if (_startingCount > 0)
            {
                isRunning = true;

                RunMultiThreadParallelTasks();

                isRunning = false;
            }

            if (onComplete != null)
                onComplete();

            yield return null;
        }

        void OnThreadedTaskDone()
        {
            lock (_locker)
            {
                --_counter;
#if TO_IMPLEMENT_PROPERLY
               _progress = (float)(_startingCount - registeredEnumerators.Count) / (float)_startingCount;
#endif

                Monitor.Pulse(_locker);
            }
            
            _countdown.Signal();
        }

        void RunMultiThreadParallelTasks()
        {
            _counter = 0;

            _countdown.AddCount(registeredEnumerators.Count);

            while (registeredEnumerators.Count > 0)
            {
                _taskRoutinePool.RetrieveTaskFromPool().SetScheduler(_runner).SetEnumeratorProvider(RunTask).Start();
                                
                lock (_locker)
                    if (++_counter >= _maxConcurrentTasks)
                        Monitor.Wait(_locker);
            }

            _countdown.Wait();
        }

        IEnumerator RunTask()
        {
            yield return registeredEnumerators.Dequeue();

            OnThreadedTaskDone();
        }
#if TO_IMPLEMENT_PROPERLY
        volatile float      _progress;
        volatile float      _totalTasks;
#endif
        uint                _maxConcurrentTasks;
        object              _locker = new object();
        Countdown           _countdown = new Countdown();
        volatile int        _counter = 0;
        int                 _startingCount;

        IPausableTaskPool   _taskRoutinePool;
        MultiThreadRunner   _runner;
    }

    public class Countdown
    {
        object _locker = new object();
        int _value;

        public Countdown() { }
        public Countdown(int initialCount) { _value = initialCount; }

        public void Signal() { AddCount(-1); }

        public void AddCount(int amount)
        {
            lock (_locker)
            {
                _value += amount;

                if (_value <= 0) Monitor.PulseAll(_locker);
            }
        }

        public void Wait()
        {
            lock (_locker)
              while (_value > 0)
                    Monitor.Wait(_locker);
        }
    }
}
#endif