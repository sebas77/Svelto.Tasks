using System;
using System.Diagnostics;
using Svelto.DataStructures;
using Console = Utility.Console;
using System.Threading;
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
        public bool paused
        {
            set; get;
        }

        public bool isStopping
        {
            get
            {
                ThreadUtility.MemoryBarrier();
                return _waitForflush == true;
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
            Kill();
        }

        public MultiThreadRunner(string name, bool relaxed = true)
        {
#if !NETFX_CORE || NET_STANDARD_2_0 || NETSTANDARD2_0
            var thread = new Thread(() =>
            {
                _name = name;

                RunCoroutineFiber();
            });

            thread.IsBackground = true;
            if (relaxed)
            {
                _lockingMechanism = RelaxedLockingMechanism;
            }
            else
            {
                _lockingMechanism = QuickLockingMechanism;
            }
#else
            var thread = new Task(() =>
            {
                _name = name;

                RunCoroutineFiber();
            });
    
            _lockingMechanism = RelaxedLockingMechanism;
#endif

            _mevent = new ManualResetEventEx();

            thread.Start();
        }

        public MultiThreadRunner(string name, int intervalInMS) : this(name, false)
        {
            _interval = intervalInMS;
            _watch = new Stopwatch();
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

        public void Kill()
        {
            _breakThread = true;
            
            UnlockThread();
            
            if (_watch != null)
                _watch.Stop();
        }

        void RunCoroutineFiber()
        {
            while (_breakThread == false)
            {
                ThreadUtility.MemoryBarrier();

				if (_newTaskRoutines.Count > 0 && false == _waitForflush) //don't start anything while flushing
                    _coroutines.AddRange(_newTaskRoutines.DequeueAll());

                for (var i = 0; i < _coroutines.Count; i++)
                {
                    var enumerator = _coroutines[i];

                    try
                    { 
#if TASKS_PROFILER_ENABLED
                        bool result = _taskProfiler.MonitorUpdateDuration(enumerator, _name);
#else
                        bool result = enumerator.MoveNext();
#endif
                        if (result == false)
                        {
                            var disposable = enumerator as IDisposable;
                            if (disposable != null)
                                disposable.Dispose();

                            _coroutines.UnorderedRemoveAt(i--);
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.InnerException != null)
                            Console.LogException(e.InnerException);
                        else
                            Console.LogException(e);

                        _coroutines.UnorderedRemoveAt(i--);
                    }
                }
                
                if (_interval > 0 && _waitForflush == false)
                {
                    WaitForInterval();
                }

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

            if (_mevent != null)
                _mevent.Dispose();
        }

#if !NETFX_CORE || NET_STANDARD_2_0 || NETSTANDARD2_0
        void WaitForInterval()
        {
            _watch.Start();
            while (_watch.ElapsedMilliseconds < _interval)
                ThreadUtility.Yield();
            
            _watch.Reset();
        }
        
        void QuickLockingMechanism()
        {
            int quickIterations = 0;

            while (Interlocked.CompareExchange(ref _interlock, 1, 1) != 1)
            {
                ThreadUtility.Yield();
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
#endif

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

        readonly FasterList<IPausableTask>      _coroutines = new FasterList<IPausableTask>();
        readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines = new ThreadSafeQueue<IPausableTask>();

        string _name;
        int    _interlock;

        volatile bool _isAlive;
        volatile bool _waitForflush;
        volatile bool _breakThread;

        readonly ManualResetEventEx _mevent;

        readonly Action    _lockingMechanism;
        readonly int       _interval;
        readonly Stopwatch _watch;

#if TASKS_PROFILER_ENABLED
        readonly TaskProfiler _taskProfiler = new TaskProfiler();
#endif
    }
}
