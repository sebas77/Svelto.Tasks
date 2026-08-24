#if ENABLE_PLATFORM_PROFILER || TASKS_PROFILER_ENABLED || (DEBUG && !PROFILE_SVELTO)
#define GENERATE_NAME
#endif

using System;
using System.Collections;
using System.Runtime.CompilerServices;
using DBC.Tasks;
using Svelto.DataStructures;

namespace Svelto.Tasks.ExtraLean
{
    /// <summary>
    /// Contains the logic shared by both ExtraLeanSveltoTask (struct/class) implementations.
    /// </summary>
    static class ExtraLeanSveltoTaskCommon
    {
#if DEBUG && !PROFILE_SVELTO
        internal static StepState Step<TEnumerator>(ref SveltoTaskState taskState,
                                                    ref TEnumerator    runningTask,
                                                    bool               isTaskStruct)
#else
        internal static StepState Step<TEnumerator>(ref SveltoTaskState taskState,
                                                    ref TEnumerator    runningTask)
#endif
            where TEnumerator : IEnumerator
        {
            Check.Require(taskState.completed == false, "ExtraLeanSveltoTask impossible state");

            bool completed;

            if (taskState.explicitlyStopped == false)
            {
                if (runningTask.MoveNext() == false)
                    completed = true;
                else
                {
                    var current = runningTask.Current;

                    if (current == null)
                        completed = false;
                    else 
                    if (current == TaskContract.Break.It || current == TaskContract.Break.AndStop)
                        completed = true;
                    else
                        throw new SveltoTaskException(
                            "ExtraLean enumerator can return only null, Yield.It, Break.It, Break.AndStop and yield break");
                }
#if DEBUG && !PROFILE_SVELTO
                if (isTaskStruct == false && runningTask == null)
                    throw new SveltoTaskException("Something went extremely wrong, has the runner been disposed?");
#endif
            }
            else
                completed = true;

            if (completed)
            {
                taskState.completed = true;
                return StepState.Completed;
            }

            return StepState.Running;
        }
    }
}

namespace Svelto.Tasks.ExtraLean
{
    namespace Struct
    {
        public struct ExtraLeanSveltoTask<TTask> : ISveltoTask where TTask : struct, IEnumerator
        {
            //Note I wonder if I should return an handle from this, to be able to stop the task externally
            internal void Run<TRunner>(TRunner runner, ref TTask task)
                    where TRunner : class, IRunner<ExtraLeanSveltoTask<TTask>>
            {
                _runningTask  = task;

#if DEBUG && !PROFILE_SVELTO
           Check.Require(runner != null, "The runner cannot be null ".FastConcat(ToString()));
#endif
                runner.AddTask(this, (-1, TombstoneHandle.Invalid));
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

            void ISveltoTask.Stop()
            {
                _threadSafeSveltoTaskStates.explicitlyStopped = true;
            }

            void ISveltoTask.Dispose()
            {
                _threadSafeSveltoTaskStates.completed = true;

                IDisposable box = s_box;

                if (box == null)
                {
                    // ThreadStatic box not been created yet.
                    s_box = box = (IDisposable)_runningTask;
                }

                // Get ref to T in Box
                ref TTask unboxed = ref Unsafe.Unbox<TTask>(box);
                // Copy to boxed ref so everything else is same
                unboxed = _runningTask;
                box.Dispose();

                _runningTask = default;
            }

            [ThreadStatic] static IDisposable s_box;

            public bool isCompleted => _threadSafeSveltoTaskStates.completed;

            public string name => ToString();

            /// <summary>
            ///     Move Next is called by the current runner, which could be on another thread! that means that the
            ///     --->class states used in this function must be thread safe<-----
            /// </summary>
            /// <param name="taskIndex"></param>
            /// <returns></returns>
            StepState ISveltoTask.Step(int runningTaskIndexFromRunningTasksToReplace, TombstoneHandle currentSpawnedTaskToRunIndex)
            {
#if DEBUG && !PROFILE_SVELTO
                return ExtraLeanSveltoTaskCommon.Step(ref _threadSafeSveltoTaskStates,
                                          ref _runningTask,
                                          IS_TASK_STRUCT);
#else
                return ExtraLeanSveltoTaskCommon.Step(ref _threadSafeSveltoTaskStates,
                    ref _runningTask);
#endif
            }


            SveltoTaskState _threadSafeSveltoTaskStates;
            TTask           _runningTask;

#if GENERATE_NAME
            string _name;
#endif
#if DEBUG && !PROFILE_SVELTO
         static readonly bool IS_TASK_STRUCT = true;
#endif
        }
    }

    public struct ExtraLeanSveltoTask<TTask> : ISveltoTask where TTask : class, IEnumerator
    {
        //Note I wonder if I should return an handle from this, to be able to stop the task externally
        internal void Run<TRunner>(TRunner runner, ref TTask task)
            where TRunner : class, IRunner<ExtraLeanSveltoTask<TTask>>
        {
            _runningTask  = task;
            
#if DEBUG && !PROFILE_SVELTO
            Check.Require(IS_TASK_STRUCT == true || task != null, 
                "A valid enumerator is required to enable an ExtraLeanSveltTask ".FastConcat(ToString()));
            Check.Require(runner != null, "The runner cannot be null ".FastConcat(ToString()));
#endif
            runner.AddTask(this, (-1, TombstoneHandle.Invalid));
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

        void ISveltoTask.Stop()
        {
            _threadSafeSveltoTaskStates.explicitlyStopped = true;
        }

        void ISveltoTask.Dispose()
        {
            _threadSafeSveltoTaskStates.completed = true;
            
            if (_runningTask is IDisposable disposable)
                disposable.Dispose();
            
            _runningTask = default;
        }

        public bool isCompleted => _threadSafeSveltoTaskStates.completed;

        public string name => ToString();

        /// <summary>
        ///     Move Next is called by the current runner, which could be on another thread! that means that the
        ///     --->class states used in this function must be thread safe<-----
        /// </summary>
        /// <param name="taskIndex"></param>
        /// <returns></returns>
        StepState ISveltoTask.Step(int runningTaskIndexFromRunningTasksToReplace, TombstoneHandle currentSpawnedTaskToRunIndex)
        {
#if DEBUG && !PROFILE_SVELTO
    return ExtraLeanSveltoTaskCommon.Step(ref _threadSafeSveltoTaskStates,
                                          ref _runningTask,
                                          IS_TASK_STRUCT);
#else
            return ExtraLeanSveltoTaskCommon.Step(ref _threadSafeSveltoTaskStates,
                ref _runningTask);
#endif
        }

        SveltoTaskState _threadSafeSveltoTaskStates;
        TTask           _runningTask;

#if GENERATE_NAME
        string _name;
#endif
#if DEBUG && !PROFILE_SVELTO
        static readonly bool IS_TASK_STRUCT = false;
#endif
    }
}