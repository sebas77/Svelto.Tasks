using System;
using System.Collections;
using Svelto.DataStructures;
using UnityEngine;
using Object = UnityEngine.Object;

//
//it doesn't make any sense to have more than one MonoRunner active
//that's why I eventually decided to keep it as a static class.
//Only downside is that I assume that the TaskRunner gameobject
//is never destroyed after it's created.
//
namespace Svelto.Tasks.Internal
{
    class MonoRunner : IRunner
    {
        public bool paused { set; get; }
        public bool stopped { get { return _stopped; } }

        public int numberOfRunningTasks { get { return _coroutines.Count; } }

        static MonoRunner()
        {
            _coroutines = new FasterList<PausableTask>(NUMBER_OF_INITIAL_COROUTINE);

            _go = new GameObject("TaskRunner");

            RunnerBehaviour runnerBehaviour = _go.AddComponent<RunnerBehaviour>();
            _runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();
            runnerBehaviour.StartCoroutine(CoroutinesRunner());

            Object.DontDestroyOnLoad(_go);
        }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes
        /// it's the final user responsability to stop the tasks if needed
        /// </summary>
        public void StopAllCoroutines() 
        {
            _stopped = true; paused = false;

            _runnerBehaviourForUnityCoroutine.StopAllCoroutines();
            
            _newTaskRoutines.Clear();

            //note: _coroutines will be cleaned by the single tasks stopping silently.
            //in this way they will be put back to the pool.
            //let's be sure that the runner had the time to stop and recycle the previous tasks
            _waitForflush = true; 
        }

        public void StartCoroutineThreadSafe(PausableTask task)
        {
            if (task == null) return; 

            paused = false;

            _newTaskRoutines.Enqueue(task); //careful this could run on another thread!
        }

        public void StartCoroutine(PausableTask task)
        {
            paused = false;

            InternalThreadUnsafeStartCoroutine(task);
        }

        static void InternalThreadUnsafeStartCoroutine(PausableTask task)
        {
            if (task == null || (_stopped == false && task.MoveNext() == false))
                return;

            _newTaskRoutines.Enqueue(task); //careful this could run on another thread!
        }

        protected static IEnumerator CoroutinesRunner()
        {
            while (true)
            {
                var newTaskRoutine = _newTaskRoutines;
                var coroutines = _coroutines;

                if (newTaskRoutine.Count > 0 && _waitForflush == false) //don't start anything while flushing
                    newTaskRoutine.DequeueAll(coroutines);

                for (int i = 0; i < coroutines.Count; i++)
                {
                    var enumerator = _coroutines[i];

                    try
                    {
                        //let's spend few words about this. Special YieldInstruction can be only processed internally
                        //by Unity. The simplest way to handle them is to hand them to Unity itself. 
                        //However while the Unity routine is processed, the rest of the coroutine is waiting for it.
                        //This would defeat the purpose of the parallel procedures. For this reason, the Parallel
                        //routines will mark the enumerator returned as ParallelYield which will change the way the routine is processed.
                        //in this case the MonoRunner won't wait for the Unity routine to continue processing the next tasks.
                        var current = enumerator.Current;
                        PausableTask enumeratorToHandle = null;
                        var yield = current as ParallelYield;
                        if (yield != null)
                            current = yield.Current;
                        else
                            enumeratorToHandle = enumerator;

                        if (current is YieldInstruction || current is AsyncOperation)
                        {
                            _runnerBehaviourForUnityCoroutine.StartCoroutine(HandItToUnity(current, enumeratorToHandle));

                            if (enumeratorToHandle != null)
                            {
                                _coroutines.UnorderredRemoveAt(i--);
                                continue;
                            }
                        }

                        bool result;
#if TASKS_PROFILER_ENABLED && UNITY_EDITOR
                        result = Svelto.Tasks.Profiler.TaskProfiler.MonitorUpdateDuration(enumerator);
#else
                        result = enumerator.MoveNext();
#endif
                        if (result == false)
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

                        Debug.LogException(new CoroutineException(message, e));

                        _coroutines.UnorderredRemoveAt(i--);
                    }
                }

                if (_waitForflush == true && coroutines.Count == 0)
                {  //this process is more complex than I like, not 100% sure it covers all the cases yet
                    _waitForflush = false;
                    _stopped = false;
                }

                yield return null;
            }
        }

        static  IEnumerator HandItToUnity(object current, PausableTask task)
        {
            yield return current;
            InternalThreadUnsafeStartCoroutine(task);
        }

        static readonly FasterList<PausableTask>     _coroutines;
        static readonly ThreadSafeQueue<PausableTask> _newTaskRoutines = new ThreadSafeQueue<PausableTask>();
        static readonly RunnerBehaviour               _runnerBehaviourForUnityCoroutine;
        static readonly GameObject                   _go;

        static bool                                  _stopped;
        static bool                                  _waitForflush;

        const int   NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}
