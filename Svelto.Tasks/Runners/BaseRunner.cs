using System;
using Svelto.DataStructures;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    /// <summary>
    /// Remember, unless you are using the StandardSchedulers, nothing hold your runners. Be careful that if you
    /// don't hold a reference, they will be garbage collected even if tasks are still running
    /// </summary>
    public abstract class BaseRunner<T> : IRunner, IRunner<T> where T: ISveltoTask
    {
        public bool isStopping => _flushingOperation.stopping;
        public bool isKilled => _flushingOperation.kill;

        public int numberOfRunningTasks => _coroutines.Count;
        public int numberOfQueuedTasks => _newTaskRoutines.Count;
        public int numberOfProcessingTasks => _newTaskRoutines.Count + _coroutines.Count;

        protected BaseRunner(string name, int size)
        {
            _name = name;
            _newTaskRoutines = new ThreadSafeQueue<T>(size);
            _coroutines = new FasterList<T>((uint) size);
        }
        
        protected BaseRunner(string name)
        {
            _name            = name;
            _newTaskRoutines = new ThreadSafeQueue<T>(NUMBER_OF_INITIAL_COROUTINE);
            _coroutines      = new FasterList<T>(NUMBER_OF_INITIAL_COROUTINE);
        }

        ~BaseRunner()
        {
            Console.LogWarning(this._name.FastConcat(" has been garbage collected, this could have serious" +
                                         "consequences, are you sure you want this? "));
            
            StopAllCoroutines();
        }

        public void Pause()
        {
            _flushingOperation.paused = true;
        }

        public void Resume()
        {
            _flushingOperation.paused = false;
        }

        public void Step()
        {
            _processEnumerator.MoveNext(false);
        }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes it's the final user responsibility to stop the tasks
        /// if needed
        /// </summary>
        public virtual void StopAllCoroutines()
        {
            CoroutineRunner<T>.StopRoutines(_flushingOperation);

            _newTaskRoutines.Clear();
        }

        void IRunner<T>.StartCoroutine(ref T task /*, bool immediate*/)
        {
            _newTaskRoutines.Enqueue(task);
            
            //if (immediate)
              //  _processEnumerator.MoveNext(true);
        }

        public virtual void Dispose()
        {
            StopAllCoroutines();

            CoroutineRunner<T>.KillProcess(_flushingOperation);
            
            GC.SuppressFinalize(this);
        }

        protected IProcessSveltoTasks _processEnumerator;
        
        protected readonly ThreadSafeQueue<T> _newTaskRoutines;
        protected readonly FasterList<T>      _coroutines;

        protected CoroutineRunner<T>.FlushingOperation _flushingOperation = new CoroutineRunner<T>.FlushingOperation();

        readonly string _name;

        const int NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}

