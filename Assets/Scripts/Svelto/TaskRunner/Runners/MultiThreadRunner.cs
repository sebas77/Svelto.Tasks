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
        public MultiThreadRunner()
        {
            paused = false;
        }

        public override string ToString()
        {
            return _name;
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
                _waitForflush = false;
                _isAlive = true;

                MemoryBarrier();
#if NETFX_CORE
                IAsyncAction asyncAction = ThreadPool.RunAsync
                    ((workItem) =>
                    {
                        _threadID = Thread.CurrentThread.ManagedThreadId;

                        RunCoroutineFiber();
                    });

#else
                ThreadPool.QueueUserWorkItem(
                    stateInfo => //creates a new thread only if there isn't any running. It's always unique
                    {
                        _threadID = Thread.CurrentThread.ManagedThreadId;

                        RunCoroutineFiber();
                    });
#endif
                _name = _threadID.ToString();
            }
        }

        public void StopAllCoroutines()
        {
            _newTaskRoutines.Clear();

            _waitForflush = true;
            MemoryBarrier();
        }

        public bool paused
        {
            set
            {
                _paused = value;
                MemoryBarrier();
            }
            get
            {
                MemoryBarrier();
                return _paused;
            }
        }

        public bool stopped
        {
            get
            {
                MemoryBarrier();
                return _isAlive == false;
            }
        }

        public int numberOfRunningTasks
        {
            get { return _coroutines.Count; }
        }

        void RunCoroutineFiber()
        {
            while (_coroutines.Count > 0 || _newTaskRoutines.Count > 0)
            {
                MemoryBarrier();
                if (_waitForflush)
                    break; //kill the thread

                if (_newTaskRoutines.Count > 0) //don't start anything while flushing
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
            }

            _isAlive = false;
            _waitForflush = false;
            MemoryBarrier();
        }

        public static void MemoryBarrier()
        {
#if NETFX_CORE
            Interlocked.MemoryBarrier();
#else
            Thread.MemoryBarrier();
#endif
        }

        readonly FasterList<IPausableTask> _coroutines = new FasterList<IPausableTask>();

        volatile bool _isAlive;
        readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines = new ThreadSafeQueue<IPausableTask>();

        bool _paused;

        volatile int  _threadID;
        volatile bool _waitForflush;
        string _name;
    }
}