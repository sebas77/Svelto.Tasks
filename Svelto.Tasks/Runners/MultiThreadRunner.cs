using System;
using System.Diagnostics;
using System.Threading;
using Svelto.DataStructures;
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
                return _runnerData._waitForflush;
            }
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

        public MultiThreadRunner(string name, bool relaxed = true, int intervalInMs = 0)
        {
            _name = name;
            var runnerData = new RunnerData(relaxed, intervalInMs, name);
            _runnerData = runnerData;
#if !NETFX_CORE || NET_STANDARD_2_0 || NETSTANDARD2_0
            //threadpool doesn't work well with Unity apparently
            //it seems to choke when too meany threads are started
            var thread = new Thread(() => runnerData.RunCoroutineFiber()) {IsBackground = true};

            thread.Start();
#else
            var thread = new Task(() => 
            {
                _name = name;

                RunCoroutineFiber(ref runnerData);
            }, TaskCreationOptions.LongRunning);

            thread.Start();
#endif
        }

        public MultiThreadRunner(string name, int intervalInMS) : this(name, false, intervalInMS)
        {}

        public void StartCoroutineThreadSafe(IPausableTask task)
        {
            StartCoroutine(task);
        }

        public void StartCoroutine(IPausableTask task)
        {
            paused = false;

            _runnerData._newTaskRoutines.Enqueue(task);
            _runnerData.UnlockThread();
        }

        public void StopAllCoroutines()
        {
            _runnerData._newTaskRoutines.Clear();
            _runnerData._waitForflush = true;

            ThreadUtility.MemoryBarrier();
        }

        public void Kill(Action onThreadKilled)
        {
            _runnerData.Kill(onThreadKilled);
        }

        class RunnerData
        {
            public RunnerData(bool relaxed, int interval, string name)
            {
                _mevent          = new ManualResetEventEx();
                _relaxed         = relaxed;
                _watch           = new Stopwatch();
                _coroutines      = new FasterList<IPausableTask>();
                _newTaskRoutines = new ThreadSafeQueue<IPausableTask>();
                _interval        = interval;
                _name = name;
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

                while (Interlocked.CompareExchange(ref _interlock, 1, 1) != 1)
                {
                    //yielding here was slower on the 1 M points simulation
                    if (++quickIterations < 1000)
                        ThreadUtility.Yield();
                    else
                        ThreadUtility.TakeItEasy();

                    //this is quite arbitrary at the moment as 
                    //DateTime allocates a lot in UWP .Net Native
                    //and stopwatch casues several issues
                    if (++quickIterations > 20000)
                    {
                        RelaxedLockingMechanism();
                        break;
                    }
                }

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

                while (_watch.ElapsedMilliseconds < _interval)
                    if (++quickIterations < 1000)
                        ThreadUtility.Yield();
                    else
                        ThreadUtility.TakeItEasy();

                _watch.Reset();
            }

            public void UnlockThread()
            {
                ThreadUtility.MemoryBarrier();
                if (_isAlive == false)
                {
                    _isAlive = true;
                    _interlock = 1;

                    _mevent.Set();

                    ThreadUtility.MemoryBarrier();
                }
            }

            public void Kill(Action onThreadKilled)
            {
                if (_mevent != null) //already disposed
                {
                    _onThreadKilled = onThreadKilled;

                    _breakThread = true;

                    UnlockThread();

                    if (_watch != null)
                    {
                        _watch.Stop();
                        _watch = null;
                    }
                }
            }

            internal void RunCoroutineFiber()
            {
                using (var platformProfiler = new PlatformProfiler(_name))
                {
                    while (_breakThread == false)
                    {
                        ThreadUtility.MemoryBarrier();
                        if (_newTaskRoutines.Count > 0 && false == _waitForflush) //don't start anything while flushing
                            _coroutines.AddRange(_newTaskRoutines.DequeueAll());

                        for (var i = 0; i < _coroutines.Count && false == _breakThread; i++)
                        {
                            var enumerator = _coroutines[i];
#if TASKS_PROFILER_ENABLED
                            bool result = Profiler.TaskProfiler.MonitorUpdateDuration(enumerator, _name);
#else
                            bool result;
                            using (platformProfiler.Sample(enumerator.ToString()))
                            {
                                result = enumerator.MoveNext();
                            }
#endif                            
                            if (result == false)
                            {
                                var disposable = enumerator as IDisposable;
                                if (disposable != null)
                                    disposable.Dispose();

                                _coroutines.UnorderedRemoveAt(i--);
                            }
                        }

                        ThreadUtility.MemoryBarrier();
                        if (_breakThread == false)
                        {
                            if (_interval > 0 && _waitForflush == false) WaitForInterval();

                            if (_coroutines.Count == 0)
                            {
                                _waitForflush = false;

                                if (_newTaskRoutines.Count == 0)
                                {
                                    _isAlive = false;

                                    if (_relaxed)
                                        RelaxedLockingMechanism();
                                    else
                                        QuickLockingMechanism();
                                }

                                ThreadUtility.MemoryBarrier();
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
            public volatile bool                           _waitForflush;

            volatile bool _isAlive;
            volatile bool _breakThread;
            
            readonly FasterList<IPausableTask> _coroutines;
            readonly int                       _interval;
            readonly bool                      _relaxed;
            readonly string _name;
            
            ManualResetEventEx _mevent;
            Action             _onThreadKilled;
            Stopwatch          _watch;
            int                _interlock;
        }

        string              _name;
        readonly RunnerData _runnerData;
    }
}