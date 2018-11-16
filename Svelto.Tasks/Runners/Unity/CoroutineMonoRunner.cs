#if UNITY_5 || UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using Svelto.DataStructures;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Unity.Internal;
using UnityEngine;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    /// while you can instantiate a MonoRunner, you should use the standard one whenever possible. Instantiating
    /// multiple runners will defeat the initial purpose to get away from the Unity monobehaviours internal updates.
    /// MonoRunners are disposable though, so at least be sure to dispose the ones that are unused
    /// CoroutineMonoRunner is the only Unity based Svelto.Tasks runner that can handle Unity YieldInstructions
    /// You should use YieldInstructions only when extremely necessary as often an Svelto.Tasks IEnumerator
    /// replacement is available.
    /// </summary>
    public class CoroutineMonoRunner : MonoRunner
    {
        public CoroutineMonoRunner(string name, bool mustSurvive = false):base(name)
        {
            _platformProfiler = new Svelto.Common.PlatformProfiler(name);
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            RunnerBehaviour runnerBehaviour = _go.AddComponent<RunnerBehaviour>();
            _runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();

            _info = new CoroutineRunningInfo(_runnerBehaviourForUnityCoroutine, _flushingOperation, _coroutines,
                                             StartCoroutine) {runnerName = name};

            runnerBehaviour.StartCoroutine(new UnityCoroutineRunner.Process<CoroutineRunningInfo>(
                _newTaskRoutines, _coroutines, _flushingOperation, _info));
        }
        
        public override void StartCoroutine(IPausableTask task)
        {
            paused = false;

            if (ExecuteFirstTaskStep(task) == true)
                _newTaskRoutines.Enqueue(task);
        }
        
        bool ExecuteFirstTaskStep(IPausableTask task)
        {
            if (task == null)
                return false;

            //if the runner is not ready to run new tasks, it cannot run immediately but it must be saved
            //in the newTaskRoutines to be executed once possible
            if (isStopping == true)
                return true;
            
#if TASKS_PROFILER_ENABLED
            return Svelto.Tasks.Profiler.TaskProfiler.MonitorUpdateDuration(task, _info.runnerName);
#else
            bool value;
            using (_platformProfiler.Sample(_info.runnerName.FastConcat("+", task.ToString())))
            {
                value = task.MoveNext();
            }

            return value;
#endif            
        }

        public override void Dispose()
        {
            _platformProfiler.Dispose();
            
            base.Dispose();
        }

        sealed class CoroutineRunningInfo : IRunningTasksInfo
        {
            public CoroutineRunningInfo(RunnerBehaviour                        runnerBehaviourForUnityCoroutine,
                                        UnityCoroutineRunner.FlushingOperation flushingOperation,
                                        FasterList<IPausableTask>              coroutines, 
                                        Action<IPausableTask>                  startCoroutine)
            {
                _runnerBehaviourForUnityCoroutine = runnerBehaviourForUnityCoroutine;
                _flushingOperation = flushingOperation;
                _coroutines = coroutines;
                _resumeOperation = startCoroutine;
            }

            public bool CanMoveNext(ref int nextIndex, object currentResult)
            {
                return true;
            }

            public bool CanProcessThis(ref int index)
            {
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
                /// to avoid the cost of two allocations per instruction. THIS MUST BE DONE AS FIRST STEP
                /// AS THE VERY FIRST RETURN OF THE ENUMERATOR CAN BE A YIELDINSTRUCTION ITSELF!
                ///

                IPausableTask pausableTask = _coroutines[index];
                var current = pausableTask.Current;
                
                if (current == null) return true;
                
                if (_flushingOperation.stopped == false)
                {
                    if (current is YieldInstruction)
                    {
                        var handItToUnity = new HandItToUnity
                            (current, pausableTask, _resumeOperation, _flushingOperation);

                        //remove the coroutine yielding the special instruction. it will be added back once Unity
                        //completes. When it's resumed use the StartCoroutine function, the first step is executed
                        //immediately, giving the chance to step beyond the current Yieldinstruction
                        _coroutines.UnorderedRemoveAt(index--);
                        
                        var coroutine = _runnerBehaviourForUnityCoroutine.StartCoroutine
                            (handItToUnity.GetEnumerator());

                        (pausableTask as PausableTask).onTaskHasBeenInterrupted += () =>
                                       {
                                           _runnerBehaviourForUnityCoroutine.StopCoroutine(coroutine);
                                           
                                           handItToUnity.Done();
                                       };

                        return _coroutines.Count > 0;
                    }
                    
#if DEBUG                    
                    var parallelTask = (current as ParallelTaskCollection.ParallelTask);

                    if (parallelTask != null &&
                        parallelTask.current is YieldInstruction)
                    {
                        throw new CoroutineMonoRunnerException("ParallelTaskCollection doesn't support Unity yield instructions");
                    }
#endif                    

                }
                else
                    _runnerBehaviourForUnityCoroutine.StopAllCoroutines();

                return true;
            }

            public void Reset()
            {}

            public string runnerName { get; set; }

            readonly float                                  _maxTasksPerIteration;
            readonly RunnerBehaviour                        _runnerBehaviourForUnityCoroutine;
            readonly UnityCoroutineRunner.FlushingOperation _flushingOperation;
            readonly FasterList<IPausableTask>              _coroutines;
            readonly Action<IPausableTask>                  _resumeOperation;
        }

        readonly CoroutineRunningInfo                  _info;
        readonly Svelto.Common.PlatformProfiler        _platformProfiler;
        readonly Action<IPausableTask>                 _resumeOperation;

        RunnerBehaviour _runnerBehaviourForUnityCoroutine;
        
        class HandItToUnity
        {
            public HandItToUnity(object                current,
                                 IPausableTask         task,
                                 Action<IPausableTask> resumeOperation,
                                 UnityCoroutineRunner.FlushingOperation     flush)
            {
                _current           = current;
                _task              = task;
                _resumeOperation   = resumeOperation;
                _isDone            = false;
                _flushingOperation = flush;
            }

            public IEnumerator GetEnumerator()
            {
                yield return _current;

                Done();
            }
            
            //The task must be added back in the collection even if
            //it's just to check if must stop
            public void Done()
            {
                _isDone = true;
                
                if (_flushingOperation != null &&
                    _flushingOperation.stopped == false &&
                    _resumeOperation != null)
                    _resumeOperation(_task);
            }

            public IEnumerator WaitUntilIsDone(IEnumerator original)
            {
                while (_isDone == false)
                    yield return null;

                original.MoveNext();
            }

            readonly object                _current;
            readonly IPausableTask         _task;
            readonly Action<IPausableTask> _resumeOperation;
            readonly UnityCoroutineRunner.FlushingOperation _flushingOperation;

            bool                       _isDone;
        }
    }

    public class CoroutineMonoRunnerException : Exception
    {
        public CoroutineMonoRunnerException(string message): base(message)
        {}
    }
}
#endif