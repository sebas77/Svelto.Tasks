using System;
using System.Threading;
using System.Timers;
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
    public class MultiThreadTimedRunner : IRunner
    {
        public MultiThreadTimedRunner(uint frequency)
        {
            paused = false;
            _timer = new System.Timers.Timer(frequency);
            _timer.AutoReset = false;
            _timer.Elapsed += RunCoroutineFiber;
            _name = _timer.ToString();
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

            MultiThreadRunner.MemoryBarrier();
            if (_timer.Enabled == false)
            {
                _waitForflush = false;

                _timer.Start();
            }
        }

        public void StopAllCoroutines()
        {
            _newTaskRoutines.Clear();

            _waitForflush = true;
            MultiThreadRunner.MemoryBarrier();
        }

        public bool paused
        {
            set
            {
                _paused = value;
                MultiThreadRunner.MemoryBarrier();
            }
            get
            {
                MultiThreadRunner.MemoryBarrier();
                return _paused;
            }
        }

        private System.Timers.Timer _timer;

        public bool isStopping
        {
            get
            {
                return _timer.Enabled;
            }
        }

        public int numberOfRunningTasks
        {
            get { return _coroutines.Count; }
        }

        void RunCoroutineFiber(object sender, EventArgs ea)
        {
            if (_coroutines.Count > 0 || (_newTaskRoutines.Count > 0 && false == _waitForflush))
            {
                MultiThreadRunner.MemoryBarrier();
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

                _timer.Start();
            }
            else
            {
                _waitForflush = false;
                _timer.Stop();
            }
        }

        readonly FasterList<IPausableTask> _coroutines = new FasterList<IPausableTask>();

         readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines = new ThreadSafeQueue<IPausableTask>();

        bool _paused;
 
        volatile bool _waitForflush;
        string _name;
    }
}