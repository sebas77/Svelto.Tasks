#if UNITY_5 || UNITY_5_3_OR_NEWER
using System;
using Svelto.DataStructures;
using Svelto.Tasks.Unity.Internal;
using UnityEngine;

#if TASKS_PROFILER_ENABLED
using Svelto.Tasks.Profiler;
#endif

namespace Svelto.Tasks.Unity
{
    /// <summary>
    /// Remember, unless you are using the StandardSchedulers, nothing hold your runners. Be careful that if you
    /// don't hold a reference, they will be garbage collected even if tasks are still running
    /// </summary>

    public abstract class MonoRunner : IRunner
    {
        public bool paused { set; get; }
        public bool isStopping { get { return _flushingOperation.stopped; } }
        public bool isKilled { get {return _go == null;} }
        public int  numberOfRunningTasks { get { return _coroutines.Count; } }
        
        public GameObject _go;

        private MonoRunner()
        {}

        protected MonoRunner(string name)
        {
            _name = name;
        }

        ~MonoRunner()
        {
            Svelto.Utilities.Console.LogWarning("MonoRunner has been garbage collected, this could have serious" +
                                                "consequences, are you sure you want this? ".FastConcat(_name));
            
            StopAllCoroutines();
        }
        
        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes
        /// it's the final user responsibility to stop the tasks if needed
        /// </summary>
        public virtual void StopAllCoroutines()
        {
            paused = false;

            UnityCoroutineRunner.StopRoutines(_flushingOperation);

            _newTaskRoutines.Clear();
        }

        public virtual void StartCoroutine(IPausableTask task)
        {
            paused = false;

            _newTaskRoutines.Enqueue(task); //careful this could run on another thread!
        }

        public virtual void Dispose()
        {
            StopAllCoroutines();
            
            GameObject.DestroyImmediate(_go);
            _go = null;
            GC.SuppressFinalize(this);
        }
        
        protected readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines = new ThreadSafeQueue<IPausableTask>();
        protected readonly FasterList<IPausableTask> _coroutines =
            new FasterList<IPausableTask>(NUMBER_OF_INITIAL_COROUTINE);
        
        protected UnityCoroutineRunner.FlushingOperation _flushingOperation =
            new UnityCoroutineRunner.FlushingOperation();

        string _name;

        const int NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}
#endif
