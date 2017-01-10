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
        public bool stopped { get { return flushingOperation.stopped; } }
        
        virtual public int  numberOfRunningTasks { get { return _info.count; } }

        virtual protected ThreadSafeQueue<PausableTask> newTaskRoutines { get { return _newTaskRoutines; } }
        virtual protected FlushingOperation flushingOperation { get { return _flushingOperation; } }

        static MonoRunner()
        {
            if (_go == null)
            {
                _go = new GameObject("TaskRunner");

                Object.DontDestroyOnLoad(_go);
            }

            var coroutines = new FasterList<PausableTask>(NUMBER_OF_INITIAL_COROUTINE);

            RunnerBehaviour runnerBehaviour = _go.AddComponent<RunnerBehaviour>();
            _runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();

            runnerBehaviour.StartCoroutine(CoroutinesRunner(_newTaskRoutines, coroutines, _flushingOperation, _info, _runnerBehaviourForUnityCoroutine));
        }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes
        /// it's the final user responsability to stop the tasks if needed
        /// </summary>
        public void StopAllCoroutines() 
        {
            flushingOperation.stopped = true; paused = false;

            StopUnityCouroutines();
            
            newTaskRoutines.Clear();

            //note: _coroutines will be cleaned by the single tasks stopping silently.
            //in this way they will be put back to the pool.
            //let's be sure that the runner had the time to stop and recycle the previous tasks
            flushingOperation.waitForflush = true; 
        }

        public void StartCoroutineThreadSafe(PausableTask task)
        {
            if (task == null) return; 

            paused = false;

            newTaskRoutines.Enqueue(task); //careful this could run on another thread!
        }

        public void StartCoroutine(PausableTask task)
        {   
            paused = false;

            InternalThreadUnsafeStartCoroutine(task, newTaskRoutines, flushingOperation);
        }

        static protected void InternalThreadUnsafeStartCoroutine(PausableTask task, ThreadSafeQueue<PausableTask> newTaskRoutines, FlushingOperation flushingOperation)
        {
            if (task == null || (flushingOperation.stopped == false && task.MoveNext() == false))
                return;

            newTaskRoutines.Enqueue(task); //careful this could run on another thread!
        }

        virtual protected void StopUnityCouroutines()
        {
            if (_runnerBehaviourForUnityCoroutine != null) //in case it has been destroyed
                _runnerBehaviourForUnityCoroutine.StopAllCoroutines();
        }

        protected static IEnumerator CoroutinesRunner(ThreadSafeQueue<PausableTask> newTaskRoutines, FasterList<PausableTask> coroutines, FlushingOperation flushingOperation, RunningTasksInfo info, RunnerBehaviour runnerBehaviourForUnityCoroutine = null)
        {
            while (true)
            {
                if (newTaskRoutines.Count > 0 && flushingOperation.waitForflush == false) //don't start anything while flushing
                    newTaskRoutines.DequeueAllInto(coroutines);

                info.count  = coroutines.Count;

                for (int i = 0; i < info.count; i++)
                {
                    var enumerator = coroutines[i];

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

                        if (runnerBehaviourForUnityCoroutine != null)
                        {
                            if (current is YieldInstruction || current is AsyncOperation)
                            {
                                runnerBehaviourForUnityCoroutine.StartCoroutine(HandItToUnity(current, enumeratorToHandle, newTaskRoutines, flushingOperation));

                                if (enumeratorToHandle != null)
                                {
                                    coroutines.UnorderredRemoveAt(i--);

                                    info.count  = coroutines.Count;

                                    continue;
                                }
                            }
                        }

                        bool result;
#if TASKS_PROFILER_ENABLED && UNITY_EDITOR
                        result = Tasks.Profiler.TaskProfiler.MonitorUpdateDuration(enumerator);
#else
                        result = enumerator.MoveNext();
#endif
                        if (result == false)
                        {
                            var disposable = enumerator as IDisposable;
                            if (disposable != null)
                                disposable.Dispose();

                            coroutines.UnorderredRemoveAt(i--);
                        }
                    }
                    catch (Exception e)
                    {
                        string message = "Coroutine Exception: ";

                        Debug.LogException(new CoroutineException(message, e));

                        coroutines.UnorderredRemoveAt(i--);
                    }

                    info.count  = coroutines.Count;
                }

                if (flushingOperation.waitForflush == true && coroutines.Count == 0)
                {  //this process is more complex than I like, not 100% sure it covers all the cases yet
                    flushingOperation.waitForflush = false;
                    flushingOperation.stopped = false;
                }

                yield return null;
            }
        }

        static IEnumerator HandItToUnity(object current, PausableTask task, ThreadSafeQueue<PausableTask> newTaskRoutines, FlushingOperation flushingOperation)
        {
            yield return current;

            InternalThreadUnsafeStartCoroutine(task, newTaskRoutines, flushingOperation);
        }

        readonly static ThreadSafeQueue<PausableTask> _newTaskRoutines = new ThreadSafeQueue<PausableTask>();
        readonly static RunnerBehaviour               _runnerBehaviourForUnityCoroutine;
        readonly static FlushingOperation             _flushingOperation = new FlushingOperation();
        readonly static RunningTasksInfo              _info = new RunningTasksInfo();
                
        protected static GameObject                   _go;

        protected class FlushingOperation
        {
            public bool stopped;
            public bool waitForflush;
        }

        protected class RunningTasksInfo
        {
            public int count;
        }

        const int   NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}
