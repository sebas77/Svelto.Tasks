using System;
using System.Threading;
using Svelto.DataStructures;
using Console = Utility.Console;

#if NETFX_CORE
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System.Threading;
#endif

namespace Svelto.Tasks
{
    //The multithread runner always uses just one thread to run all the couroutines
    //If you want to use a separate thread, you will need to create another MultiThreadRunner
    public class MultiThreadRunner : IRunner
    {
        public override string ToString()
        {
            return _name;
        }

        public MultiThreadRunner(bool relaxed)
        {
            _thread = new Thread(() =>
            {
                _threadID = Thread.CurrentThread.ManagedThreadId;
                _name = _threadID.ToString();

                RunCoroutineFiber();
            });

            _thread.IsBackground = true;
            _thread.Start();

            if (relaxed)
                _lockingMechanism = RelaxedLockingMechanism;
            else
                _lockingMechanism = QuickLockingMechanism;

            _relaxed = relaxed;

            _aevent = new AutoResetEvent(false);
        }

        void QuickLockingMechanism()
        {
            while (Interlocked.CompareExchange(ref _interlock, 1, 1) != 1)
#if NET_4_6
            { Thread.Yield(); } 
#else
            { Thread.Sleep(0); }
#endif
        }
    
        void RelaxedLockingMechanism()
        {
            _aevent.WaitOne();
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

        private void UnlockThread()
        {
            if (_relaxed)
                _aevent.Set();
            else
            {
                _interlock = 1;
                MemoryBarrier();
            }
        }

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
                    _interlock = 2;

                    _lockingMechanism();
                }
            }
        }

        public static void MemoryBarrier()
        {
#if NETFX_CORE || NET_4_6
            Interlocked.MemoryBarrier();
#else
            Thread.MemoryBarrier();
#endif
        }

        readonly FasterList<IPausableTask> _coroutines = new FasterList<IPausableTask>();
        readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines = new ThreadSafeQueue<IPausableTask>();

        Thread _thread;
        string _name;

        volatile bool _paused;
        volatile bool _isAlive;
        volatile int  _threadID;
        volatile bool _waitForflush;
        volatile bool _breakThread;
        int _interlock;

        AutoResetEvent _aevent;
        System.Action _lockingMechanism;
        private bool _relaxed;
    }
}
