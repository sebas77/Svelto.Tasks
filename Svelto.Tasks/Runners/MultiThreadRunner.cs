using System;
using System.Diagnostics;
using System.Threading;
using Svelto.DataStructures;
using Svelto.Tasks.Unity.Internal;
using Svelto.Utilities;

#if NETFX_CORE
using System.Threading.Tasks;
#endif

namespace Svelto.Tasks
{
    //The multithread runner always uses just one thread to run all the couroutines
    //If you want to use a separate thread, you will need to create another MultiThreadRunner
    public sealed class MultiThreadRunner : IRunner
    {
        public bool paused { set; get; }

        public bool isStopping
        {
            get
            {
                ThreadUtility.MemoryBarrier();
                return _runnerData._waitForFlush;
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

        public override string ToString()
        {
            return _name;
        }

        ~MultiThreadRunner()
        {
            Dispose();
        }

        public void Dispose()
        {
            Kill(null);
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

            Init(name, runnerData);
        }

        /// <summary>
        /// Start a Multithread runner that won't take 100% of the CPU
        /// </summary>
        /// <param name="name"></param>
        /// <param name="intervalInMs"></param>
        public MultiThreadRunner(string name, float intervalInMs)
        {
            var runnerData = new RunnerData(true, intervalInMs, name, false);

            Init(name, runnerData);
        }

        void Init(string name, RunnerData runnerData)
        {
            _name       = name;
            _runnerData = runnerData;
#if !NETFX_CORE
            //threadpool doesn't work well with Unity apparently
            //it seems to choke when too meany threads are started
            new Thread(() => runnerData.RunCoroutineFiber()) {IsBackground = true}.Start();
#else
            Task.Factory.StartNew(() => runnerData.RunCoroutineFiber(), TaskCreationOptions.LongRunning);
#endif
        }

        public void StartCoroutine(IPausableTask task)
        {
            if (_runnerData == null)
                throw new MultiThreadRunnerException("Trying to start a task on a killed runner");
            
            paused = false;

            _runnerData._newTaskRoutines.Enqueue(task);
            _runnerData.UnlockThread();
        }

        public void StopAllCoroutines()
        {
            if (_runnerData == null)
                throw new MultiThreadRunnerException("Trying to stop tasks on a killed runner");
            
            _runnerData._newTaskRoutines.Clear();
            _runnerData._waitForFlush = true;

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
                _coroutines          = new FasterList<IPausableTask>();
                _newTaskRoutines     = new ThreadSafeQueue<IPausableTask>();
                _interval            = (long) (interval * 10000);
                _name                = name;
                _isRunningTightTasks = isRunningTightTasks;

                if (relaxed)
                    LockingMechanism = RelaxedLockingMechanism;
                else
                    LockingMechanism = QuickLockingMechanism;
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

                while (_watch.ElapsedTicks < _interval)
                {
                    ThreadUtility.Wait(ref quickIterations);

                    if (ThreadUtility.VolatileRead(ref _breakThread) == true) return;
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
                using (var platformProfiler = new Svelto.Common.PlatformProfilerMT(_name))
                {
                    while (_breakThread == false)
                    {
                        ThreadUtility.MemoryBarrier();
                        if (_newTaskRoutines.Count > 0 && false == _waitForFlush) //don't start anything while flushing
                            _newTaskRoutines.DequeueAllInto(_coroutines);

                        IPausableTask pausableTask;
                        bool          result;
                        
                        for (var index = 0;
                             index < _coroutines.Count && false == ThreadUtility.VolatileRead(ref _breakThread); ++index)
#if ENABLE_PLATFORM_PROFILER                            
                            using (platformProfiler.Sample(_coroutines[index].ToString()))
#endif
                            {
                                pausableTask = _coroutines[index];
                                result = pausableTask.MoveNext();

                                if (result == false)
                                {
                                    var disposable = pausableTask as IDisposable;
                                    if (disposable != null)
                                        disposable.Dispose();

                                    _coroutines.UnorderedRemoveAt(index--);
                                }
                            }

                        if (ThreadUtility.VolatileRead(ref _breakThread) == false)
                        {
                            if (_interval > 0 && _waitForFlush == false)
                                WaitForInterval();

                            if (_coroutines.Count == 0)
                            {
                                _waitForFlush = false;

                                if (_newTaskRoutines.Count == 0)
                                    LockingMechanism();

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

            public readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines;
            public volatile bool                           _waitForFlush;

            bool _breakThread;

            readonly FasterList<IPausableTask> _coroutines;
            readonly long                      _interval;
            readonly string                    _name;
            readonly bool                      _isRunningTightTasks;

            ManualResetEventEx     _mevent;
            Action                 _onThreadKilled;
            Stopwatch              _watch;
            int                    _interlock;
            readonly System.Action LockingMechanism;
            int                    _yieldingCount;
        }

        string     _name;
        RunnerData _runnerData;
    }

    public class MultiThreadRunnerException : Exception
    {
        public MultiThreadRunnerException(string message): base(message)
        {}
    }
}