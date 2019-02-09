using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using Svelto.DataStructures;
using Svelto.Utilities;

#if NETFX_CORE
using System.Threading.Tasks;
#endif

namespace Svelto.Tasks
{
    public sealed class MultiThreadRunner:MultiThreadRunner<IEnumerator>
    {
        public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false) : base(name, relaxed, tightTasks)
        {
        }

        public MultiThreadRunner(string name, float intervalInMs) : base(name, intervalInMs)
        {
        }
    }
    //The multithread runner always uses just one thread to run all the couroutines
    //If you want to use a separate thread, you will need to create another MultiThreadRunner
    public class MultiThreadRunner<T> : IRunner<T> where T:IEnumerator
    {
        public bool isPaused
        {
            get
            {
                return _runnerData.isPaused;
            }
            set { _runnerData.isPaused = value; }
        }

        public bool isStopping
        {
            get
            {
                ThreadUtility.MemoryBarrier();
                return _runnerData.waitForFlush;
            }
        }

        public bool isKilled
        {
            get { return _runnerData == null; }
        }

        public int numberOfRunningTasks
        {
            get { return _runnerData.Count; }
        }
        
        public int numberOfQueuedTasks
        {
            get { return  _runnerData.newTaskRoutines.Count; }
        }

        public override string ToString()
        {
            return _runnerData.name;
        }

        ~MultiThreadRunner()
        {
            Console.LogWarning("MultiThreadRunner has been garbage collected, this could have serious" +
                                                        "consequences, are you sure you want this? ".FastConcat(_runnerData.name));
                                                        
            Dispose();
        }

        public void Dispose()
        {
            if (_runnerData != null)
                Kill(null);
            
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// when the thread must run very tight and cache friendly tasks that won't
        /// allow the CPU to start new threads, passing the tightTasks as true
        /// would force the thread to yield after every iteration. Relaxed to true
        /// would let the runner be less reactive on new tasks added  
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tightTasks"></param>
        public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false)
        {
            var runnerData = new RunnerData(relaxed, 0, name, tightTasks);

            Init(runnerData);
        }

        /// <summary>
        /// Start a Multithread runner that won't take 100% of the CPU
        /// </summary>
        /// <param name="name"></param>
        /// <param name="intervalInMs"></param>
        public MultiThreadRunner(string name, float intervalInMs)
        {
            var runnerData = new RunnerData(true, intervalInMs, name, false);

            Init(runnerData);
        }

        void Init(RunnerData runnerData)
        {
            _runnerData = runnerData;
#if !NETFX_CORE
            //threadpool doesn't work well with Unity apparently
            //it seems to choke when too meany threads are started
            new Thread(() => runnerData.RunCoroutineFiber()) {IsBackground = true}.Start();
#else
            Task.Factory.StartNew(() => runnerData.RunCoroutineFiber(), TaskCreationOptions.LongRunning);
#endif
        }

        public void StartCoroutine(ISveltoTask<T> task)
        {
            if (_runnerData == null)
                throw new MultiThreadRunnerException("Trying to start a task on a killed runner");
            
            isPaused = false;

            _runnerData.newTaskRoutines.Enqueue(task);
            _runnerData.UnlockThread();
        }

        public void StopAllCoroutines()
        {
            if (_runnerData == null)
                throw new MultiThreadRunnerException("Trying to stop tasks on a killed runner");
            
            _runnerData.newTaskRoutines.Clear();
            _runnerData.waitForFlush = true;

            ThreadUtility.MemoryBarrier();
        }

        public void Kill(Action onThreadKilled)
        {
            if (_runnerData == null)
                throw new MultiThreadRunnerException("Trying to kill an already killed runner");
            
            _runnerData.Kill(onThreadKilled);
            _runnerData = null;
        }

        class RunnerData
        {
            public RunnerData(bool relaxed, float interval, string name, bool isRunningTightTasks)
            {
                _mevent              = new ManualResetEventEx();
                _watch               = new Stopwatch();
                _coroutines          = new FasterList<ISveltoTask<T>>();
                newTaskRoutines      = new ThreadSafeQueue<ISveltoTask<T>>();
                _intervalInTicks     = (long) (interval * Stopwatch.Frequency / 1000);
                this.name            = name;
                _isRunningTightTasks = isRunningTightTasks;

                if (relaxed)
                    _lockingMechanism = RelaxedLockingMechanism;
                else
                    _lockingMechanism = QuickLockingMechanism;
            }

            public int Count
            {
                get
                {
                    ThreadUtility.MemoryBarrier();

                    return _coroutines.Count;
                }
            }    

            void QuickLockingMechanism()
            {
                var quickIterations = 0;
                var frequency       = 1024;

                while (ThreadUtility.VolatileRead(ref _interlock) != 1 && quickIterations < 4096)
                {
                    ThreadUtility.Wait(ref quickIterations, frequency);

                    if (ThreadUtility.VolatileRead(ref _breakThread) == true)
                        return;
                }

                if (_interlock == 0 && ThreadUtility.VolatileRead(ref _breakThread) == false)
                    RelaxedLockingMechanism();
                else
                    _interlock = 0;
            }

            void RelaxedLockingMechanism()
            {
                _mevent.Wait();

                _mevent.Reset();
            }

            void WaitForInterval()
            {
                var quickIterations = 0;
                _watch.Start();

                while (_watch.ElapsedTicks < _intervalInTicks)
                {
                    ThreadUtility.Wait(ref quickIterations, 1024);

                    if (ThreadUtility.VolatileRead(ref _breakThread) == true) return;
                }

                _watch.Reset();
            }

            internal void UnlockThread()
            {
                if (_mevent == null)
                    return;
                
                _interlock = 1;

                _mevent.Set();

                ThreadUtility.MemoryBarrier();
            }

            public void Kill(Action onThreadKilled)
            {
                if (_mevent != null) //already disposed
                {
                    _onThreadKilled = onThreadKilled;
                    _breakThread    = true;
                    ThreadUtility.MemoryBarrier();

                    UnlockThread();
                }

                if (_watch != null)
                {
                    _watch.Stop();
                    _watch = null;
                }
            }

            internal void RunCoroutineFiber()
            {
#if ENABLE_PLATFORM_PROFILER
                var platformProfiler = new Common.PlatformProfilerMT();
                using (platformProfiler.StartNewSession(name))
#endif    
                {
                    while (_breakThread == false)
                    {
                        ThreadUtility.MemoryBarrier();
                        if (newTaskRoutines.Count > 0 && false == waitForFlush) //don't start anything while flushing
                            newTaskRoutines.DequeueAllInto(_coroutines);

                        var coroutines = _coroutines.ToArrayFast();

                        for (var index = 0;
                             index < _coroutines.Count && false == ThreadUtility.VolatileRead(ref _breakThread);
                             ++index)
                        {
                            bool result;
#if ENABLE_PLATFORM_PROFILER
                            using (platformProfiler.Sample(coroutines[index].ToString()))
#endif
                            {
                                if (waitForFlush) coroutines[index].Stop();

#if TASKS_PROFILER_ENABLED
                                result =
                                    Profiler.TaskProfiler.MonitorUpdateDuration(coroutines[index], name);
#else
                                result = coroutines[index].MoveNext();
#endif

                                if (result == false)
                                    _coroutines.UnorderedRemoveAt(index--);
                            }
                        }

                        if (ThreadUtility.VolatileRead(ref _breakThread) == false)
                        {
                            if (_intervalInTicks > 0 && waitForFlush == false)
                                WaitForInterval();

                            if (_coroutines.Count == 0)
                            {
                                waitForFlush = false;

                                if (newTaskRoutines.Count == 0 || isPaused == true)
                                    _lockingMechanism();

                                ThreadUtility.MemoryBarrier();
                            }
                            else
                            {
                                if (_isRunningTightTasks)
                                    ThreadUtility.Wait(ref _yieldingCount, 16);
                            }
                        }
                    }

                    if (_onThreadKilled != null)
                        _onThreadKilled();

                    if (_mevent != null)
                    {
                        _mevent.Dispose();
                        _mevent = null;

                        ThreadUtility.MemoryBarrier();
                    }
                }
            }

            internal readonly ThreadSafeQueue<ISveltoTask<T>> newTaskRoutines;
            internal volatile bool                            waitForFlush;
            internal bool isPaused
            {
                get { return _isPaused; }
                set
                {
                    if (value == false) UnlockThread();
                    
                    _isPaused = value;
                } 
            }

            readonly FasterList<ISveltoTask<T>> _coroutines;
            readonly long                       _intervalInTicks;
            readonly bool                       _isRunningTightTasks;
            readonly Action                     _lockingMechanism;
            
            internal string name;

            ManualResetEventEx _mevent;
            Action             _onThreadKilled;
            Stopwatch          _watch;
            int                _interlock;
            int                _yieldingCount;
            bool               _isPaused;
            bool               _breakThread;
        }

        RunnerData _runnerData;
    }

    public class MultiThreadRunnerException : Exception
    {
        public MultiThreadRunnerException(string message): base(message)
        {}
    }
}