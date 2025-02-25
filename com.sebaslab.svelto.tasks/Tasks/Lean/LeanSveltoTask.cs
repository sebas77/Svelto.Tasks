using System.Collections.Generic;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.Lean
{
    public struct LeanSveltoTask<TTask> : ISveltoTask where TTask : IEnumerator<TaskContract>
    {
        internal Continuation Run<TRunner>(TRunner runner, in TTask task)
            where TRunner : class, IRunner<LeanSveltoTask<TTask>>
        {
            _sveltoTask = new SveltoTaskWrapper<TTask, IRunner<LeanSveltoTask<TTask>>>(task, runner);

#if DEBUG && !PROFILE_SVELTO
             DBC.Tasks.Check.Require(IS_TASK_STRUCT == true || task != null
                                  , "A valid enumerator is required to enable a LeanSveltTask ".FastConcat(ToString()));

            _continuation = new Continuation(ContinuationPool.RetrieveFromPool(), runner);
#else
            _continuation = new Continuation(ContinuationPool.RetrieveFromPool());
#endif

            runner.AddTask(this, (-1, -1));

            return _continuation;
        }
        
        //This is now meant to be used ALWAYS FROM THE SAME RUNNER OF THE PARENT TASK THAT IS SPAWNING THIS TASK
        internal void SpawnContinuingTask<TRunner>(  TRunner runner, in TTask task, Continuation continuation, (int runningTaskIndexToReplace, int parentSpawnedTaskIndex) index)
            where TRunner : class, IRunner<LeanSveltoTask<TTask>>
        {
            _sveltoTask = new SveltoTaskWrapper<TTask, IRunner<LeanSveltoTask<TTask>>>(in task, runner);
#if DEBUG && !PROFILE_SVELTO
            DBC.Tasks.Check.Require(IS_TASK_STRUCT == true || task != null
                                  , "A valid enumerator is required to enable a LeanSveltTask ".FastConcat(ToString()));
#endif

            _continuation = continuation;

            runner.AddTask(this, index);
        }

        public override string ToString()
        {
            return name;
        }

        public void Stop()
        {
            _threadSafeSveltoTaskStates.explicitlyStopped = true; //will be completed next step
        }

        public bool isCompleted => _threadSafeSveltoTaskStates.completed;

        public string name => _sveltoTask.name;

        public StepState Step(int runningTaskIndexToReplace, int parentSpawnedTaskIndex)
        {
            DBC.Tasks.Check.Require(_threadSafeSveltoTaskStates.completed == false, "impossible state");
            StepState stepState = StepState.Running;

            try
            {
                if (_threadSafeSveltoTaskStates.explicitlyStopped == false)
                    stepState = _sveltoTask.Step(runningTaskIndexToReplace, parentSpawnedTaskIndex);
                else
                    stepState = StepState.Completed;
            }
            finally
            {
                if (stepState == StepState.Completed)
                {
                    _continuation.ReturnToPool();
                    _threadSafeSveltoTaskStates.completed = true;
                }
            }

            return stepState;
        }

        SveltoTaskWrapper<TTask, IRunner<LeanSveltoTask<TTask>>> _sveltoTask;
        SveltoTaskState                                          _threadSafeSveltoTaskStates;
        Continuation                                             _continuation;

#if DEBUG && !PROFILE_SVELTO
        static readonly bool IS_TASK_STRUCT = typeof(TTask).IsValueType;
#endif
    }
}