#if ENABLE_PLATFORM_PROFILER || TASKS_PROFILER_ENABLED || (DEBUG && !PROFILER)
#define GENERATE_NAME
#endif

using System;
using System.Collections.Generic;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    public struct LeanSveltoTask<TTask>: ISveltoTask where TTask : IEnumerator<TaskContract>
    {
        internal ContinuationEnumerator Start<TRunner>(TRunner runner, ref TTask task, bool immediate) where TRunner: class, IInternalRunner<LeanSveltoTask<TTask>>
        {
#if DEBUG && !PROFILER                        
            DBC.Tasks.Check.Require(IS_TASK_STRUCT == true || task != null, 
                                    "A valid enumerator is required to enable a LeanSveltTask ".FastConcat(ToString()));
   
            DBC.Tasks.Check.Require(runner != null, "SetScheduler function has never been called");
#endif            
    
#if GENERATE_NAME
            _name = task.ToString();
#endif
            _continuationEnumerator               = ContinuationWrapperPool.RetrieveFromPool();
            _sveltoTask                          = new SveltoTaskWrapper<TTask, IInternalRunner<LeanSveltoTask<TTask>>>(ref task, runner);
            _threadSafeSveltoTaskStates.started = true;
            
            runner.StartCoroutine(ref this, immediate);

            return _continuationEnumerator;
        }
        
        public override string ToString()
        {
#if GENERATE_NAME
            if (_name == null)
                _name = base.ToString();
    
            return _name;
#else
            return "LeanSveltoTask";
#endif            
        }

        public void Stop()
        {
            _threadSafeSveltoTaskStates.explicitlyStopped = true;
        }

        public TaskContract Current
        {
            get { return _sveltoTask.Current; }
        }
        
        public bool MoveNext()
        {
            DBC.Tasks.Check.Require(_threadSafeSveltoTaskStates.completed == false,
                                    "ExtraLeanSveltoTask impossible state ".FastConcat(ToString()));
            bool completed;
            if (_threadSafeSveltoTaskStates.explicitlyStopped == false)
            {
                try
                {
                    completed = !_sveltoTask.MoveNext();
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
                _continuationEnumerator.Completed();
                ContinuationWrapperPool.PushBack(_continuationEnumerator);
                _continuationEnumerator                  = null;
                _threadSafeSveltoTaskStates.completed = true;
                        
                return false;
            }

            return true;
        }

        SveltoTaskWrapper<TTask, IInternalRunner<LeanSveltoTask<TTask>>> _sveltoTask;
        SveltoTaskState                                                  _threadSafeSveltoTaskStates;
        ContinuationEnumerator                                              _continuationEnumerator;
#if GENERATE_NAME
        string _name;
#endif
#if DEBUG && !PROFILER
        static readonly bool IS_TASK_STRUCT = typeof(TTask).IsValueType;
#endif

    }
}