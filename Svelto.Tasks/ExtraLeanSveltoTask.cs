#if ENABLE_PLATFORM_PROFILER || TASKS_PROFILER_ENABLED || (DEBUG && !PROFILER)
#define GENERATE_NAME
#endif

using System;
using System.Collections;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    public struct ExtraLeanSveltoTask<TTask> : ISveltoTask where TTask : IEnumerator
    {
        internal ContinuationEnumerator Run<TRunner>(TRunner runner, ref TTask task, bool immediate)
            where TRunner : class, IInternalRunner<ExtraLeanSveltoTask<TTask>>
        {
#if DEBUG && !PROFILER            
            DBC.Tasks.Check.Require(IS_TASK_STRUCT == true || task != null, 
                "A valid enumerator is required to enable an ExtraLeanSveltTask ".FastConcat(ToString()));
#endif    

            DBC.Tasks.Check.Require(runner != null, "The runner cannot be null ".FastConcat(ToString()));

#if GENERATE_NAME
            _name = task.ToString();
#endif
            _threadSafeSveltoTaskStates.started = true;
            _runningTask = task;

            runner.StartCoroutine(ref this, immediate);

            return null;
        }

        public override string ToString()
        {
#if GENERATE_NAME
            if (_name == null)
                _name = base.ToString();
    
            return _name;
#else
            return "ExtraLeanSveltoTask";
#endif            
        }

        public void Stop()
        {
            _threadSafeSveltoTaskStates.explicitlyStopped = true;
        }

        public TaskContract Current
        {
            get { return Yield.It; }
        }

        /// <summary>
        /// Move Next is called by the current runner, which could be on another thread! that means that the
        /// --->class states used in this function must be thread safe<-----
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
                DBC.Tasks.Check.Require(_threadSafeSveltoTaskStates.completed == false,
                                        "ExtraLeanSveltoTask impossible state ".FastConcat(ToString()));

                    bool completed;
                    if (_threadSafeSveltoTaskStates.explicitlyStopped == false)
                    {
                        try
                        {
                            completed = !_runningTask.MoveNext();
                        }
                        catch (Exception e)
                        {
                            completed = true;
                            
                            Console.LogException("a Svelto.Tasks task threw an exception at:  "
                                                              .FastConcat(ToString()), e);
                        }
                    }
                    else
                        completed = true;

                    if (completed == true)
                    {
                        _threadSafeSveltoTaskStates.completed = true;
                        
                        return false;
                    }

                return true;
        }

        SveltoTaskState _threadSafeSveltoTaskStates;
        TTask           _runningTask;

#if GENERATE_NAME
        string                _name;
#endif
#if DEBUG && !PROFILER
        static readonly bool IS_TASK_STRUCT = typeof(TTask).IsValueType;
#endif
    }
}