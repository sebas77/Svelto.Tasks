using System;
using System.Diagnostics;
using Svelto.DataStructures;
using Console = Utility.Console;
using System.Threading;

#if NETFX_CORE
using System.Threading.Tasks;
#endif

namespace Svelto.Tasks
{
    //The multithread runner always uses just one thread to run all the couroutines
    //If you want to use a separate thread, you will need to create another MultiThreadRunner
    public class MultiThreadRunner : IRunner
    {
        public bool paused
        {
            set
            {
                _paused = value;
            }
            get
            {
                return _paused;
            }
        }

        public bool isStopping
        {
            get
            {
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

        public MultiThreadRunner(bool relaxed = true)
        {
#if !NETFX_CORE
            _thread = new Thread(() =>
            {
                _threadID = Thread.CurrentThread.ManagedThreadId;
                _name = _threadID.ToString();

                RunCoroutineFiber();
            });

            _thread.IsBackground = true;
#else
            _thread = new Task(() =>
            {
                _threadID = (int)Task.CurrentId;
                _name = _threadID.ToString();

                RunCoroutineFiber();
            });
#endif
            _thread.Start();

            if (relaxed)
            {                
                _lockingMechanism = RelaxedLockingMechanism;
#if NET_4_6 || NETFX_CORE
                _aevent = new ManualResetEventSlim(false);
#else
                _aevent = new ManualResetEvent(false);
#endif
            }
            else
                _lockingMechanism = QuickLockingMechanism;

            _relaxed = relaxed;
        }

        public MultiThreadRunner(int intervalInMS) : this(false)
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

            MemoryBarrier();
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
            
            MemoryBarrier();
        }

        public void Kill()
        {
            _breakThread = true;
            UnlockThread();
        }

        public static void MemoryBarrier()
        {
#if NETFX_CORE || NET_4_6
            Interlocked.MemoryBarrier();
#else
            Thread.MemoryBarrier();
#endif
        }

        void RunCoroutineFiber()
        {
            while (_breakThread == false)
            {
                MemoryBarrier();

				if (_newTaskRoutines.Count > 0 && false == _waitForflush) //don't start anything while flushing
                    _coroutines.AddRange(_newTaskRoutines.DequeueAll());

                for (var i = 0; i < _coroutines.Count; i++)
                {
                    var enumerator = _coroutines[i];

                    try
                    {
#if TASKS_PROFILER_ENABLED
                        bool result = Profiler.TaskProfiler.MonitorUpdateDuration(enumerator, _threadID);
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

                if (_newTaskRoutines.Count == 0 && _coroutines.Count == 0)
                {
                    _isAlive = false;
                    _waitForflush = false;

                    _lockingMechanism();
                }
                else
                if (_interval > 0)
                {
                    _waitForInterval();
                }
            }

            if (_aevent != null)
#if !(NETFX_CORE || NET_4_6)
                _aevent.Close();
#else
                _aevent.Dispose();
#endif
        }

        void _waitForInterval()
        {
            _watch.Start();
            while (_watch.ElapsedMilliseconds < _interval)
#if NETFX_CORE
            { Task.Yield(); }
#elif NET_4_6
            { Thread.Yield(); } 
#else
            { Thread.Sleep(0); }
#endif
            _watch.Reset();
        }

        void QuickLockingMechanism()
        {
            _interlock = 2;

            while (Interlocked.CompareExchange(ref _interlock, 1, 1) != 1)
#if NETFX_CORE
            { Task.Yield(); }
#elif NET_4_6

            { Thread.Yield(); } 
#else
            { Thread.Sleep(0); }
#endif
        }

        void RelaxedLockingMechanism()
        {
#if NETFX_CORE || NET_4_6
            _aevent.Wait();
#else
            _aevent.WaitOne();
#endif
            _aevent.Reset();
        }


        void UnlockThread()
        {
            if (_relaxed)
                _aevent.Set();
#if !NETFX_CORE
            else
            {
                _interlock = 1;
                MemoryBarrier();
            }
#endif
        }

        readonly FasterList<IPausableTask> _coroutines = new FasterList<IPausableTask>();
        readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines = new ThreadSafeQueue<IPausableTask>();
#if NETFX_CORE
        Task _thread;
#else
        Thread _thread;
#endif
        string _name;

        volatile bool _paused;
        volatile bool _isAlive;
        volatile int  _threadID;
        volatile bool _waitForflush;
        volatile bool _breakThread;
        int _interlock;

#if NETFX_CORE || NET_4_6
        ManualResetEventSlim _aevent;
#else
        ManualResetEvent _aevent;
#endif
        Action _lockingMechanism;
        TimeSpan _timeSpan;
        bool _relaxed;
        int _interval;
        Stopwatch _watch;
    }
}
