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

        public void StartCoroutine(IEnumerator task)
        {	
            stopped = false;
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
                    StartCoroutineInternal();

                    _isAlive = false;
                    Thread.MemoryBarrier();
                });
            }
        }

        void StartCoroutineInternal()
        {
            while (_coroutines.Count > 0 || _newTaskRoutines.Count > 0)
            {
                while (_newTaskRoutines.Count > 0)
                    _coroutines.AddRange(_newTaskRoutines.DequeueAll());

                for (int i = 0; i < _coroutines.Count; i++)
                {
                    var enumerator = _coroutines[i];

                    try
                    {
                        if (enumerator.MoveNext() == false)
                        {
                            Thread.MemoryBarrier();
                            if (stopped == true)
                            {
                                _coroutines.DeepClear();
    
                                return; //will kill the thread
                            }

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
            }
        }

        public void StopAllCoroutines()
        {
            _newTaskRoutines.Clear();

            stopped = true;
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
    }
}
