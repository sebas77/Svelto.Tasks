using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Svelto.Common;
using Svelto.DataStructures;
using Svelto.Tasks.Internal;
using Svelto.Utilities;

#if NETFX_CORE
using System.Threading.Tasks;
#endif

namespace Svelto.Tasks
{
    public sealed class MultiThreadRunner:MultiThreadRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
    {
        public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false) : base(name, relaxed, tightTasks)
        {
        }

        public MultiThreadRunner(string name, float intervalInMs) : base(name, intervalInMs)
        {
        }
    }

    public class MultiThreadRunner<TTask> : MultiThreadRunner<TTask, StandardRunningTasksInfo> where TTask : ISveltoTask
    {
        public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false) : 
            base(name, new StandardRunningTasksInfo(), relaxed, tightTasks)
        {
        }

        public MultiThreadRunner(string name, float intervalInMs) : base(name, new StandardRunningTasksInfo(), intervalInMs)
        {
        }
    }
    /// <summary>
    /// The multithread runner always uses just one thread to run all the couroutines
    /// If you want to use a separate thread, you will need to create another MultiThreadRunner 
    /// </summary>
    /// <typeparam name="TTask"></typeparam>
    /// <typeparam name="TFlowModifier"></typeparam>
    public class MultiThreadRunner<TTask, TFlowModifier> : IRunner, IInternalRunner<TTask> where TTask: ISveltoTask
                                                                                           where TFlowModifier:IRunningTasksInfo
    {
        /// <summary>
        /// when the thread must run very tight and cache friendly tasks that won't allow the CPU to start new threads,
        /// passing the tightTasks as true would force the thread to yield every so often. Relaxed to true
        /// would let the runner be less reactive on new tasks added.  
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tightTasks"></param>
        public MultiThreadRunner(string name, TFlowModifier modifier, bool relaxed = false, bool tightTasks = false)
        {
            var runnerData = new RunnerData(relaxed, 0, name, tightTasks, modifier);

            Init(runnerData);
        }

        /// <summary>
        /// Start a Multithread runner that won't take 100% of the CPU
        /// </summary>
        /// <param name="name"></param>
        /// <param name="intervalInMs"></param>
        public MultiThreadRunner(string name, TFlowModifier modifier, float intervalInMs)
        {
            var runnerData = new RunnerData(true, intervalInMs, name, false, modifier);

            Init(runnerData);
        }
        
        public void Pause()
        {
            _runnerData.isPaused = true;
        }

        public void Resume()
        {
            _runnerData.isPaused = false;
        }
        
        public bool paused
        {
            get
            {
                return _runnerData.isPaused;
            }
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

        public int numberOfProcessingTasks
        {
            get { return _runnerData.Count + _runnerData.newTaskRoutines.Count; }
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
            if (isKilled == false)
                Kill(null);
            
            GC.SuppressFinalize(this);
        }

        void Init(RunnerData runnerData)
        {
            _runnerData = runnerData;
#if !NETFX_CORE
            //threadpool doesn't work well with Unity apparently it seems to choke when too meany threads are started
            new Thread(() => runnerData.RunCoroutineFiber()) {IsBackground = true}.Start();
#else
            Task.Factory.StartNew(() => runnerData.RunCoroutineFiber(), TaskCreationOptions.LongRunning);
#endif
        }

        public void StartCoroutine(ref TTask task, bool immediate)
        {
            if (isKilled == true)
                throw new MultiThreadRunnerException("Trying to start a task on a killed runner");
            
            _runnerData.newTaskRoutines.Enqueue(task);
            _runnerData.UnlockThread();
        }

        public void StopAllCoroutines()
        {
            if (isKilled == true)
                throw new MultiThreadRunnerException("Trying to stop tasks on a killed runner");
            
            _runnerData.newTaskRoutines.Clear();
            _runnerData.waitForFlush = true;

            ThreadUtility.MemoryBarrier();
        }

        internal void Kill(Action onThreadKilled)
        {
            if (isKilled == true)
                throw new MultiThreadRunnerException("Trying to kill an already killed runner");
            
            _runnerData.Kill(onThreadKilled);
            _runnerData = null;
        }
        
        public void Kill()
        {
            if (isKilled == true)
                throw new MultiThreadRunnerException("Trying to kill an already killed runner");
            
            _runnerData.Kill(null);
            _runnerData = null;
        }
        
        RunnerData _runnerData;

        class RunnerData
        {
            public RunnerData(bool          relaxed, float interval, string name, bool isRunningTightTasks,
                              TFlowModifier modifier)
            {
                _mevent              = new ManualResetEventEx();
                _watch               = new Stopwatch();
                _coroutines          = new FasterList<TTask>();
                newTaskRoutines      = new ThreadSafeQueue<TTask>();
                _interval            = (long) (interval * 10000);
                this.name            = name;
                _isRunningTightTasks = isRunningTightTasks;
                _flushingOperation   = new CoroutineRunner<TTask>.FlushingOperation();
                modifier.runnerName  = name;
                _process             = new CoroutineRunner<TTask>.Process<TFlowModifier,
                    PlatformProfilerMT>(newTaskRoutines, _coroutines, _flushingOperation, modifier); 

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

                    if (ThreadUtility.VolatileRead(ref _flushingOperation.kill) == true)
                        return;
                }

                if (_interlock == 0 && ThreadUtility.VolatileRead(ref _flushingOperation.kill) == false)
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

                while (_watch.ElapsedTicks < _interval)
                {
                    ThreadUtility.Wait(ref quickIterations);

                    if (ThreadUtility.VolatileRead(ref _flushingOperation.kill) == true) return;
                }

                _watch.Reset();
            }

            internal void UnlockThread()
            {
                _interlock = 1;

                _mevent.Set();

                ThreadUtility.MemoryBarrier();
            }

            public void Kill(Action onThreadKilled)
            {
                if (_mevent != null) //already disposed
                {
                    _onThreadKilled         = onThreadKilled;
                    _flushingOperation.kill = true;
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
                ThreadUtility.MemoryBarrier();
                
                while (_process.MoveNext(false))
                {
                    if (_flushingOperation.kill == false)
                    {
                        if (_flushingOperation.paused)
                            _lockingMechanism();
                                
                        if (_interval > 0)
                            WaitForInterval();

                        if (_coroutines.Count == 0)
                        {
                            if (newTaskRoutines.Count == 0)
                                _lockingMechanism();
                            else
                                ThreadUtility.Wait(ref _yieldingCount, 16);
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
            
            internal bool isPaused
            {
                get { return _flushingOperation.paused; }
                set
                {
                    ThreadUtility.VolatileWrite(ref _flushingOperation.paused, value);
                    
                    if (value == false) UnlockThread();
                }
            }

            internal bool waitForFlush
            {
                get { return _flushingOperation.stopping; }
                set
                {
                    ThreadUtility.VolatileWrite(ref _flushingOperation.stopping, value);
                }
            }

            internal readonly ThreadSafeQueue<TTask> newTaskRoutines;
            internal readonly string                 name;

            readonly FasterList<TTask> _coroutines;
            readonly long              _interval;
            readonly bool              _isRunningTightTasks;
            readonly System.Action     _lockingMechanism;

            ManualResetEventEx _mevent;
            Action             _onThreadKilled;
            Stopwatch          _watch;
            int                _interlock;
            int                _yieldingCount;

            readonly CoroutineRunner<TTask>.FlushingOperation                          _flushingOperation;
            readonly CoroutineRunner<TTask>.Process<TFlowModifier, PlatformProfilerMT> _process;
        }
    }

    public class MultiThreadRunnerException : Exception
    {
        public MultiThreadRunnerException(string message): base(message)
        {}
    }
}