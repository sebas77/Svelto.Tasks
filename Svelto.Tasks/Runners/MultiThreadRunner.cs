using System;
using System.Diagnostics;
using System.Threading;
using Svelto.DataStructures;
using Svelto.Utilities;

#if TASKS_PROFILER_ENABLED
using Svelto.Tasks.Profiler;
#endif
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
                return _waitForflush;
            }
        }

        public int numberOfRunningTasks
        {
            get { return _coroutines.Count; }
        }

        public override string ToString()
        {
            return _name;
        }

        public void Dispose()
        {
            Kill(null);
        }

        public MultiThreadRunner(string name, bool relaxed = true)
        {
            _mevent = new ManualResetEventEx();

            if (relaxed)
                _lockingMechanism = RelaxedLockingMechanism;
            else
                _lockingMechanism = QuickLockingMechanism;

#if !NETFX_CORE || NET_STANDARD_2_0 || NETSTANDARD2_0
            //threadpool doesn't work well with Unity apparently
            //it seems to choke when too meany threads are started
            var thread = new Thread(() =>
                                    {
                                        _name = name;

                                        RunCoroutineFiber();
                                    });

            thread.IsBackground = true;

            thread.Start();
#else
            var thread = new Task(() => 
            {
                _name = name;

                RunCoroutineFiber();
            }, TaskCreationOptions.LongRunning);

            thread.Start();
#endif
        }

        public MultiThreadRunner(string name, int intervalInMS) : this(name, false)
        {
            _interval = intervalInMS;
            _watch    = new Stopwatch();
        }

        public void StartCoroutineThreadSafe(IPausableTask task)
        {
            StartCoroutine(task);
        }

        public void StartCoroutine(IPausableTask task)
        {
            paused = false;

            _newTaskRoutines.Enqueue(task);

            ThreadUtility.MemoryBarrier();
            if (_isAlive == false)
            {
                _isAlive = true;

                UnlockThread();
            }
        }

        public void StopAllCoroutines()
        {
            _newTaskRoutines.Clear();

            _waitForflush = true;

            ThreadUtility.MemoryBarrier();
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

        void RunCoroutineFiber()
        {
            while (_breakThread == false)
            {
                ThreadUtility.MemoryBarrier();
                if (_newTaskRoutines.Count > 0 && false == _waitForflush) //don't start anything while flushing
                    _coroutines.AddRange(_newTaskRoutines.DequeueAll());

                for (var i = 0; i < _coroutines.Count && (false == _breakThread); i++)
                {
                    var enumerator = _coroutines[i];

#if TASKS_PROFILER_ENABLED
                    bool result = _taskProfiler.MonitorUpdateDuration(enumerator, _name);
#else
                    var result = enumerator.MoveNext();
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

                            _lockingMechanism();
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

        void WaitForInterval()
        {
            _watch.Start();
            while (_watch.ElapsedMilliseconds < _interval)
                ThreadUtility.Yield();

            _watch.Reset();
        }

        void QuickLockingMechanism()
        {
            var quickIterations = 0;

            while (Interlocked.CompareExchange(ref _interlock, 1, 1) != 1)
            {   //yielding here was slower on the 1 M points simulation
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

        void UnlockThread()
        {
            _interlock = 1;

            _mevent.Set();

            ThreadUtility.MemoryBarrier();
        }

        readonly FasterList<IPausableTask>      _coroutines      = new FasterList<IPausableTask>();
        readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines = new ThreadSafeQueue<IPausableTask>();

        string _name;
        int    _interlock;

        volatile bool _isAlive;
        volatile bool _waitForflush;
        volatile bool _breakThread;

        ManualResetEventEx _mevent;

        readonly Action _lockingMechanism;
        readonly int    _interval;
        Stopwatch       _watch;
        Action          _onThreadKilled;

#if TASKS_PROFILER_ENABLED
        readonly TaskProfiler _taskProfiler = new TaskProfiler();
#endif
    }
}