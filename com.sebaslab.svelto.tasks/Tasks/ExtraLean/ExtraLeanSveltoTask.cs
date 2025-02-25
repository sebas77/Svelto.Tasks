#if ENABLE_PLATFORM_PROFILER || TASKS_PROFILER_ENABLED || (DEBUG && !PROFILE_SVELTO)
#define GENERATE_NAME
#endif

using System.Collections;
using DBC.Tasks;

namespace Svelto.Tasks.ExtraLean
{
    public struct ExtraLeanSveltoTask<TTask> : ISveltoTask where TTask : IEnumerator
    {
        internal void Run<TRunner>(TRunner runner, ref TTask task)
            where TRunner : class, IRunner<ExtraLeanSveltoTask<TTask>>
        {
            _runningTask  = task;
            
#if DEBUG && !PROFILE_SVELTO
            Check.Require(IS_TASK_STRUCT == true || task != null, 
                "A valid enumerator is required to enable an ExtraLeanSveltTask ".FastConcat(ToString()));
            Check.Require(runner != null, "The runner cannot be null ".FastConcat(ToString()));
#endif
            runner.AddTask(this, (-1, -1));
        }

        public override string ToString()
        {
#if GENERATE_NAME
            if (_name == null)
                _name = _runningTask.ToString();

            return _name;
#else
            return "ExtraLeanSveltoTask";
#endif
        }

        public void Stop()
        {
            _threadSafeSveltoTaskStates.explicitlyStopped = true;
        }

        public bool isCompleted => _threadSafeSveltoTaskStates.completed;

        public string name => ToString();

        /// <summary>
        ///     Move Next is called by the current runner, which could be on another thread! that means that the
        ///     --->class states used in this function must be thread safe<-----
        /// </summary>
        /// <param name="taskIndex"></param>
        /// <returns></returns>
        public StepState Step(int runningTaskIndexToReplace, int currentSpawnedTaskToRunIndex)
        {
            Check.Require(_threadSafeSveltoTaskStates.completed == false, "ExtraLeanSveltoTask impossible state ");

            bool completed;
            if (_threadSafeSveltoTaskStates.explicitlyStopped == false)
            {
                if (_runningTask.MoveNext() == false)
                    completed = true;
                else
                {
                    var current = _runningTask.Current;

                    if (current == null)
                        completed = false;
                    else
                    if (current == TaskContract.Break.It || current == TaskContract.Break.AndStop)
                        completed = true;
                    else
                        throw new SveltoTaskException("ExtraLean enumerator can return only null, Yield.It, Break.It, Break.AndStop and yield break");    
                }
#if DEBUG && !PROFILE_SVELTO
                if (IS_TASK_STRUCT == false && _runningTask == null)
                    throw new SveltoTaskException($"Something went extremely wrong, has the runner been disposed?");
#endif
            }
            else
                completed = true;

            if (completed == true)
            {
                _threadSafeSveltoTaskStates.completed = true;

                return StepState.Completed;
            }

            return StepState.Running;
        }

        SveltoTaskState _threadSafeSveltoTaskStates;
        TTask           _runningTask;

#if GENERATE_NAME
        string _name;
#endif
#if DEBUG && !PROFILE_SVELTO
        static readonly bool IS_TASK_STRUCT = typeof(TTask).IsValueTypeEx();
#endif
    }
}