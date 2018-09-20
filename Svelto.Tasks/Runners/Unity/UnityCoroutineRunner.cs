#if UNITY_5 || UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using Svelto.DataStructures;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Svelto.Tasks.Internal.Unity
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

        internal static void InitializeGameObject(string name, ref GameObject go, bool mustSurvive)
        {
            var taskRunnerName = "TaskRunner.".FastConcat(name);

            DBC.Tasks.Check.Require(GameObject.Find(taskRunnerName) == null, GAMEOBJECT_ALREADY_EXISTING_ERROR);

            go = new GameObject(taskRunnerName);

            if (mustSurvive)
                Object.DontDestroyOnLoad(go);
        }

        internal class Process : IEnumerator
        {
            readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines;
            readonly FasterList<IPausableTask>      _coroutines;
            readonly FlushingOperation              _flushingOperation;
            readonly RunningTasksInfo               _info;
            readonly FlushTasksDel                  _flushTaskDel;
            readonly RunnerBehaviour                _runnerBehaviourForUnityCoroutine;
            readonly Action<IPausableTask>          _resumeOperation;

            public Process( ThreadSafeQueue<IPausableTask> newTaskRoutines,
                            FasterList<IPausableTask>      coroutines, 
                            FlushingOperation              flushingOperation,
                            RunningTasksInfo               info,
                            FlushTasksDel                  flushTaskDel,
                            RunnerBehaviour                runnerBehaviourForUnityCoroutine = null,
                            Action<IPausableTask>          resumeOperation = null)
            {
                _newTaskRoutines = newTaskRoutines;
                _coroutines = coroutines;
                _flushingOperation = flushingOperation;
                _info = info;
                _flushTaskDel = flushTaskDel;
                _runnerBehaviourForUnityCoroutine = runnerBehaviourForUnityCoroutine;
                _resumeOperation = resumeOperation;
            }    

            public bool MoveNext()
            {
                if (false == _flushingOperation.stopped) //don't start anything while flushing
                    _flushTaskDel(_newTaskRoutines, _coroutines, _flushingOperation);
                else
                if (_runnerBehaviourForUnityCoroutine != null)
                    _runnerBehaviourForUnityCoroutine.StopAllCoroutines();

                UnityEngine.Profiling.Profiler.BeginSample(_info.runnerName);

                for (int i = 0; _info.MoveNext(ref i, _coroutines.Count) && i < _coroutines.Count; ++i)
                {
                    var pausableTask = _coroutines[i];

                    //let's spend few words on this. yielded YieldInstruction and AsyncOperation can
                    //only be processed internally by Unity. The simplest way to handle them is to hand them to Unity
                    //itself. However while the Unity routine is processed, the rest of the coroutine is waiting for it.
                    //This would defeat the purpose of the parallel procedures. For this reason, a Parallel task will
                    //mark the enumerator returned as ParallelYield which will change the way the routine is processed.
                    //in this case the MonoRunner won't wait for the Unity routine to continue processing the next
                    //tasks. Note that it is much better to return wrap AsyncOperation around custom IEnumerator classes
                    //then returning them directly as most of the time they don't need to be handled by Unity as
                    //YieldInstructions do

                    ///
                    /// Handle special Unity instructions you should avoid them or wrap them around custom IEnumerator
                    /// to avoid the cost of two allocations per instruction
                    /// 

                    if (_runnerBehaviourForUnityCoroutine != null && _flushingOperation.stopped == false)
                    {
                        var current = pausableTask.Current;

                        if (current is YieldInstruction)
                        {
                            var handItToUnity = new HandItToUnity
                                (current, pausableTask, _resumeOperation, _flushingOperation);

                            //remove the special instruction. it will
                            //be added back once Unity completes.
                            _coroutines.UnorderedRemoveAt(i--);

                            var coroutine = _runnerBehaviourForUnityCoroutine.StartCoroutine
                                (handItToUnity.GetEnumerator());

                            (pausableTask as PausableTask).onExplicitlyStopped = () =>
                            {
                                _runnerBehaviourForUnityCoroutine.StopCoroutine(coroutine);
                                handItToUnity.ForceStop();
                            };
                            
                            continue;
                        }

                        var parallelTask = (current as ParallelTaskCollection.ParallelTask);

                        if (parallelTask != null && 
                            parallelTask.current is YieldInstruction)
                        {
                            var handItToUnity = new HandItToUnity(parallelTask.current);

                            parallelTask.Add(handItToUnity.WaitUntilIsDone());

                            var coroutine = _runnerBehaviourForUnityCoroutine.StartCoroutine
                                (handItToUnity.GetEnumerator());
                            
                            (pausableTask as PausableTask).onExplicitlyStopped = () =>
                            {
                                _runnerBehaviourForUnityCoroutine.StopCoroutine(coroutine);
                                handItToUnity.ForceStop();
                            };
                        }
                    }                        

                    bool result;
#if TASKS_PROFILER_ENABLED
                   result = Svelto.Tasks.Profiler.TaskProfiler.MonitorUpdateDuration(pausableTask, _info.runnerName);
#else 
#if PROFILER
                   UnityEngine.Profiling.Profiler.BeginSample(_info.runnerName.FastConcat("+",pausableTask.ToString()));
#endif                    
                   result = pausableTask.MoveNext();
#if PROFILER                    
                   UnityEngine.Profiling.Profiler.EndSample();
#endif                    
#endif
                   if (result == false)
                   {
                       var disposable = pausableTask as IDisposable;
                       if (disposable != null)
                           disposable.Dispose();

                       _coroutines.UnorderedRemoveAt(i--);
                   }
                }

                UnityEngine.Profiling.Profiler.EndSample();
                
                if (_flushingOperation.stopped == true && _coroutines.Count == 0)
                {   //once all the coroutines are flushed
                    //the loop can return accepting new tasks
                    _flushingOperation.stopped = false;
                }

                return true;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public object Current { get; private set; }
        }

        public class RunningTasksInfo
        {
            public string runnerName;

            public virtual bool MoveNext(ref int index, int count)
            {
                return true;
            }
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

                ForceStop();
            }
            
            public void ForceStop()
            {
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

            bool                       _isDone;
            readonly FlushingOperation _flushingOperation;
        }
        
        const string GAMEOBJECT_ALREADY_EXISTING_ERROR = "A MonoRunner GameObject with the same name was already been used, did you forget to dispose the old one?";
    }
}
#endif