#if UNITY_5 || UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using Svelto.DataStructures;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Svelto.Tasks.Internal
{
    public static class UnityCoroutineRunner
    {
        public static void StandardTasksFlushing(ThreadSafeQueue<IPausableTask> newTaskRoutines, 
            FasterList<IPausableTask> coroutines, FlushingOperation flushingOperation)
        {
            if (newTaskRoutines.Count > 0)
                newTaskRoutines.DequeueAllInto(coroutines);
        }

        public static void StopRoutines(FlushingOperation
            flushingOperation)
        {
            //note: _coroutines will be cleaned by the single tasks stopping silently.
            //in this way they will be put back to the pool.
            //let's be sure that the runner had the time to stop and recycle the previous tasks
            flushingOperation.stopped = true;
        }

        internal static GameObject InitializeGameobject(string name)
        {
            var taskRunnerName = "TaskRunner.".FastConcat(name);
            var go = GameObject.Find(taskRunnerName);

            if (go != null)
            {
                // Destroy the old object before making a new one
                Object.DestroyImmediate(go);
            }

            go = new GameObject(taskRunnerName);
#if UNITY_EDITOR
            if (Application.isPlaying)
#endif
                Object.DontDestroyOnLoad(go);

            return go;
        }

        internal static IEnumerator Process(ThreadSafeQueue<IPausableTask> newTaskRoutines,
                                            FasterList<IPausableTask> coroutines, 
                                            FlushingOperation flushingOperation,
                                            RunningTasksInfo info, 
                                            FlushTasksDel flushTaskDel)
        {
            return Process(newTaskRoutines, coroutines, 
                flushingOperation, info, flushTaskDel, null, null);
        }

        internal static IEnumerator Process(
            ThreadSafeQueue<IPausableTask> newTaskRoutines,
            FasterList<IPausableTask> coroutines, 
            FlushingOperation flushingOperation,
            RunningTasksInfo info,
            FlushTasksDel flushTaskDel,
            RunnerBehaviour runnerBehaviourForUnityCoroutine,
            Action<IPausableTask> 
            resumeOperation)
        {
            while (true)
            {
                if (flushingOperation.stopped == false) //don't start anything while flushing
                    flushTaskDel(newTaskRoutines, coroutines, flushingOperation);
                else
                if (runnerBehaviourForUnityCoroutine != null)
                    runnerBehaviourForUnityCoroutine.StopAllCoroutines();

                info.count = coroutines.Count;

                for (var i = 0; i < info.count; i++)
                {
                    var enumerator = coroutines[i];

                    try
                    {
                        //let's spend few words about this. 
                        //yielded YieldInstruction and AsyncOperation can 
                        //only be processed internally by Unity. 
                        //The simplest way to handle them is to hand them to Unity itself.
                        //However while the Unity routine is processed, the rest of the 
                        //coroutine is waiting for it. This would defeat the purpose 
                        //of the parallel procedures. For this reason, a Parallel
                        //task will mark the enumerator returned as ParallelYield which 
                        //will change the way the routine is processed.
                        //in this case the MonoRunner won't wait for the Unity routine 
                        //to continue processing the next tasks.
                        //Note that it is much better to return wrap AsyncOperation around
                        //custom IEnumerator classes then returning them directly as
                        //most of the time they don't need to be handled by Unity as
                        //YieldInstructions do
                        
                        ///
                        /// Handle special Unity instructions
                        /// you should avoid them or wrap them
                        /// around custom IEnumerator to avoid
                        /// the cost of two allocations per instruction
                        /// 

                        if (runnerBehaviourForUnityCoroutine != null && 
                            flushingOperation.stopped == false)
                        {
                            var current = enumerator.Current;

                            if (current is YieldInstruction)
                            {
                                var handItToUnity = new HandItToUnity
                                    (current, enumerator, resumeOperation, flushingOperation);

                                //remove the special instruction. it will
                                //be added back once Unity completes.
                                coroutines.UnorderedRemoveAt(i--);

                                info.count = coroutines.Count;

                                runnerBehaviourForUnityCoroutine.StartCoroutine
                                    (handItToUnity.GetEnumerator());

                                continue;
                            }

                            var parallelTask = (current as ParallelTaskCollection.ParallelTask);

                            if (parallelTask != null && 
                                parallelTask.current is YieldInstruction)
                            {
                                var handItToUnity = new HandItToUnity(parallelTask.current);

                                parallelTask.Add(handItToUnity.WaitUntilIsDone());

                                runnerBehaviourForUnityCoroutine.StartCoroutine
                                    (handItToUnity.GetEnumerator());
                            }
                        }                        

                        bool result;
#if TASKS_PROFILER_ENABLED && UNITY_EDITOR
                        result = Tasks.Profiler.TaskProfiler.MonitorUpdateDuration(enumerator, info.runnerName);
#else
                        result = enumerator.MoveNext();
#endif
                        if (result == false)
                        {
                            var disposable = enumerator as IDisposable;
                            if (disposable != null)
                                disposable.Dispose();

                            coroutines.UnorderedRemoveAt(i--);
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.InnerException != null)
                            Utility.Console.LogException(e.InnerException);
                        else
                            Utility.Console.LogException(e);

                        coroutines.UnorderedRemoveAt(i--);
                    }

                    info.count = coroutines.Count;
                }

                if (flushingOperation.stopped == true && coroutines.Count == 0)
                {   //once all the coroutines are flushed
                    //the loop can return accepting new tasks
                    flushingOperation.stopped = false;
                }

                yield return null;
            }
        }

        public class RunningTasksInfo
        {
            public int count;
            public string runnerName;
        }

        internal delegate void FlushTasksDel(ThreadSafeQueue<IPausableTask> 
            newTaskRoutines, FasterList<IPausableTask> coroutines, 
            FlushingOperation flushingOperation);

        public class FlushingOperation
        {
            public bool stopped;
        }

        struct HandItToUnity
        {
            public HandItToUnity(object current,
                IPausableTask task,
                Action<IPausableTask> resumeOperation,
                FlushingOperation flush)
            {
                _current = current;
                _task = task;
                _resumeOperation = resumeOperation;
                _isDone = false;
                _flushingOperation = flush;
            }

            public HandItToUnity(object current)
            {
                _current = current;
                _resumeOperation = null;
                _task = null;
                _isDone = false;
                _flushingOperation = null;
            }

            public IEnumerator GetEnumerator()
            {
                yield return _current;

                _isDone = true;

                if (_flushingOperation != null && 
                    _flushingOperation.stopped == false && 
                    _resumeOperation != null)
                    _resumeOperation(_task);
            }

            public IEnumerator WaitUntilIsDone()
            {
                while (_isDone == false)
                    yield return null;
            }

            readonly object                _current;
            readonly IPausableTask         _task;
            readonly Action<IPausableTask> _resumeOperation;

            bool              _isDone;
            FlushingOperation _flushingOperation;
        }
    }
}
#endif