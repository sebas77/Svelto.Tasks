using System;
using System.Collections;
using System.Threading;
using Svelto.DataStructures;
#if NETFX_CORE
using System.Threading.Tasks;
#endif

namespace Svelto.Tasks
{
    //The multithread runner always uses just one thread to run all the couroutines
    //If you want to use a separate thread, you will need to create another MultiThreadRunner
    public class MultiThreadRunner: IRunner
    {
        public MultiThreadRunner()
        {
            paused = false;	
            stopped = false;
        }

        public void StartCoroutineThreadSafe(PausableTask task)
        {
            StartCoroutine(task);
        }

        public void StartCoroutine(PausableTask task)
        {
            paused = false;

            _newTaskRoutines.Enqueue(task);

            MemoryBarrier();            

            if (_isAlive == false)
            {
                _isAlive = true;
                MemoryBarrier();            

#if NETFX_CORE
            Task.Run
                (() =>
                {
                    RunCoroutineFiber();

                    _isAlive = false;
                    stopped = false;
                    Interlocked.MemoryBarrier();         
                }
                );

#else
                ThreadPool.QueueUserWorkItem((stateInfo) => //creates a new thread only if there isn't any running. It's always unique
                {
                    RunCoroutineFiber();

                    _isAlive = false;
                    stopped = false;
                    MemoryBarrier();            
                });
#endif
            }
        }

        void RunCoroutineFiber()
        {
            while (_coroutines.Count > 0 || _newTaskRoutines.Count > 0)
            {
                if (_newTaskRoutines.Count > 0 && _waitForflush == false) //don't start anything while flushing
                    _coroutines.AddRange(_newTaskRoutines.DequeueAll());

                for (int i = 0; i < _coroutines.Count; i++)
                {
                    var enumerator = _coroutines[i];

                    try
                    {
                        if (enumerator.MoveNext() == false)
                        {
                            var disposable = enumerator as IDisposable;
                            if (disposable != null)
                                disposable.Dispose(); 

                            _coroutines.UnorderredRemoveAt(i--);
                        }
                    }
                    catch (Exception e)
                    {
                        string message = "Coroutine Exception: ";

                        UnityEngine.Debug.LogException(new CoroutineException(message, e));

                        _coroutines.UnorderredRemoveAt(i--);
                    }
                }

                MemoryBarrier();            
                if (_waitForflush == true && _coroutines.Count == 0)
                    break; //kill the thread
            }

            _waitForflush = false;
        }

        public void StopAllCoroutines()
        {
            _newTaskRoutines.Clear();

            stopped = true;
            _waitForflush = true;
            MemoryBarrier();            
        }

        public bool paused { set { _paused = value; MemoryBarrier(); } get { MemoryBarrier(); return _paused; } }

        private void MemoryBarrier()
        {
#if NETFX_CORE
            Interlocked.MemoryBarrier();
#else
            Thread.MemoryBarrier();
#endif
        }

        public bool stopped { private set { _stopped = value; MemoryBarrier(); } get {MemoryBarrier(); return _stopped; } }
        public int numberOfRunningTasks { get { return _coroutines.Count; } }

        FasterList<IEnumerator>         _coroutines = new FasterList<IEnumerator>();
        ThreadSafeQueue<IEnumerator>    _newTaskRoutines = new ThreadSafeQueue<IEnumerator>();

        bool                            _paused;

        volatile bool _stopped;
        volatile bool _isAlive;
        volatile bool _waitForflush;
    }
}
