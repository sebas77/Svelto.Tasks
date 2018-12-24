using System;
using System.Collections.Generic;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    public struct LeanSveltoTask<TTask>: ISveltoTask where TTask : IEnumerator<TaskContract>
    {
        internal ContinuationWrapper Start<TRunner>(TRunner runner, ref TTask task, bool immediate) where TRunner: class, IInternalRunner<LeanSveltoTask<TTask>>
        {
#if DEBUG && !PROFILER                        
            DBC.Tasks.Check.Require(IS_TASK_STRUCT == true || task != null, 
                                    "A valid enumerator is required to enable a LeanSveltTask ".FastConcat(ToString()));
   
            DBC.Tasks.Check.Require(runner != null, "SetScheduler function has never been called");
#endif            
    
#if GENERATE_NAME
            _name = task.ToString();
#endif
            _continuationWrapper               = ContinuationWrapperPool.RetrieveFromPool();
            _sveltoTask                          = new SveltoTaskWrapper<TTask, IInternalRunner<LeanSveltoTask<TTask>>>(ref task, runner);
            _threadSafeSveltoTaskStates.started = true;
            
            runner.StartCoroutine(ref this, immediate);

            return _continuationWrapper;
        }
        
        public override string ToString()
        {
#if GENERATE_NAME
            if (_name == null)
                _name = base.ToString();
    
            return _name;
#endif
            return "LeanSveltoTask";
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
                        
                    Utilities.Console.LogException("a Svelto.Tasks task threw an exception at:  "
                                                      .FastConcat(ToString()), e);
                }
            }
            else
                completed = true;

            if (completed == true)
            {
                _continuationWrapper.Completed();
                ContinuationWrapperPool.PushBack(_continuationWrapper);
                _continuationWrapper                  = null;
                _threadSafeSveltoTaskStates.completed = true;
                        
                return false;
            }

            return true;
        }


        SveltoTaskWrapper<TTask, IInternalRunner<LeanSveltoTask<TTask>>> _sveltoTask;
        SveltoTaskState                   _threadSafeSveltoTaskStates;
        ContinuationWrapper               _continuationWrapper;
#if DEBUG && !PROFILER
        static readonly bool IS_TASK_STRUCT = typeof(TTask).IsValueType;
#endif

    }
}