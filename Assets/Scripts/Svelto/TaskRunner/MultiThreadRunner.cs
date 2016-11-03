using System;
using System.Collections;
using System.Threading;
using Svelto.DataStructures;

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

            //Task.Factory.StartNew(() => {}, TaskCreationOptions.LongRunning); for WSA

            _newTaskRoutines.Enqueue(task);

            Thread.MemoryBarrier();
            if (_isAlive == false)
            {
                _isAlive = true;
                Thread.MemoryBarrier();

                ThreadPool.QueueUserWorkItem((stateInfo) => //creates a new thread only if there isn't any running. It's always unique
                {
                    RunCoroutineFiber();

                    _isAlive = false;
                    stopped = false;
                    Thread.MemoryBarrier();
                });
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

                Thread.MemoryBarrier();
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
            Thread.MemoryBarrier();
        }

        public bool paused { set { _paused = value; Thread.MemoryBarrier(); } get { Thread.MemoryBarrier(); return _paused; } }
        public bool stopped { private set { _stopped = value; Thread.MemoryBarrier(); } get { Thread.MemoryBarrier(); return _stopped; } }
        public int numberOfRunningTasks { get { return _coroutines.Count; } }

        FasterList<IEnumerator>         _coroutines = new FasterList<IEnumerator>();
        ThreadSafeQueue<IEnumerator>    _newTaskRoutines = new ThreadSafeQueue<IEnumerator>();

        bool                            _paused;

        volatile bool _stopped;
        volatile bool _isAlive;
        volatile bool _waitForflush;
    }
}
