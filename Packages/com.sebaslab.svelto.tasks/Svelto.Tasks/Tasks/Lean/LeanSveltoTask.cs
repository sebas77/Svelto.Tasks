using System.Collections.Generic;
using Svelto.DataStructures;
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

            runner.AddTask(this, (-1, TombstoneHandle.Invalid));

            return _continuation;
        }
        
        //This is now meant to be used ALWAYS FROM THE SAME RUNNER OF THE PARENT TASK THAT IS SPAWNING THIS TASK
        internal void SpawnContinuingTask<TRunner>(  TRunner runner, in TTask task, Continuation continuation, (int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex) index)
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

        void ISveltoTask.Stop()
        {
            _threadSafeSveltoTaskStates.explicitlyStopped = true; //will be completed next step
        }

        void ISveltoTask.Dispose() 
        { 
            _sveltoTask.Dispose();
            _continuation.ReturnToPool();
            _threadSafeSveltoTaskStates.completed = true;
        }

        public bool isCompleted => _threadSafeSveltoTaskStates.completed;

        public string name => _sveltoTask.name;

        StepState ISveltoTask.Step(int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex)
        {
            DBC.Tasks.Check.Require(_threadSafeSveltoTaskStates.completed == false, "impossible state");
            StepState stepState = StepState.Running;

            if (_threadSafeSveltoTaskStates.explicitlyStopped == false)
                stepState = _sveltoTask.Step(runningTaskIndexToReplace, parentSpawnedTaskIndex);
            else
                stepState = StepState.Completed;
      

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