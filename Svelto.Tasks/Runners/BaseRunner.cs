using System;
using Svelto.DataStructures;
using Svelto.Tasks.Internal;

#if TASKS_PROFILER_ENABLED
using Svelto.Tasks.Profiler;
#endif

namespace Svelto.Tasks
{
    /// <summary>
    /// Remember, unless you are using the StandardSchedulers, nothing hold your runners. Be careful that if you
    /// don't hold a reference, they will be garbage collected even if tasks are still running
    /// </summary>
    public abstract class BaseRunner<T> : IRunner, IInternalRunner<T> where T: ISveltoTask
    {
        public bool isPaused
        {
            get { return _flushingOperation.paused; }
            set { _flushingOperation.paused = value; }
        }

        public bool isStopping           { get { return _flushingOperation.stopping; } }
        public bool isKilled             { get {return _flushingOperation.kill;} }
        
        public int numberOfRunningTasks    { get { return _coroutines.Count; } }
        public int numberOfQueuedTasks     { get { return _newTaskRoutines.Count; } }
        public int numberOfProcessingTasks { get { return _newTaskRoutines.Count + _coroutines.Count; }}

        protected BaseRunner(string name, int size)
        {
            _name = name;
            _newTaskRoutines = new ThreadSafeQueue<T>(size);
            _coroutines = new FasterList<T>(size);
        }
        
        protected BaseRunner(string name)
        {
            _name            = name;
            _newTaskRoutines = new ThreadSafeQueue<T>(NUMBER_OF_INITIAL_COROUTINE);
            _coroutines      = new FasterList<T>(NUMBER_OF_INITIAL_COROUTINE);
        }

        ~BaseRunner()
        {
            Utilities.Console.LogWarning("BaseRunner has been garbage collected, this could have serious" +
                                         "consequences, are you sure you want this? ".FastConcat(_name));
            
            StopAllCoroutines();
        }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes it's the final user responsibility to stop the tasks
        /// if needed
        /// </summary>
        public virtual void StopAllCoroutines()
        {
            isPaused = false;

            CoroutineRunner<T>.StopRoutines(_flushingOperation);

            _newTaskRoutines.Clear();
        }

        public void StartCoroutine(ref T task, bool immediate)
        {
            isPaused = false;

            _newTaskRoutines.Enqueue(task);
            
            if (immediate)
                _processEnumerator.MoveNext(true);
        }
        
        public void Step()
        {
            _processEnumerator.MoveNext();
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

        protected CoroutineRunner<T>.FlushingOperation  
            _flushingOperation = new CoroutineRunner<T>.FlushingOperation();

        readonly string _name;

        const int NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}

