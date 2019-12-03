#if ENABLE_PLATFORM_PROFILER || TASKS_PROFILER_ENABLED || (DEBUG && !PROFILER)
#define GENERATE_NAME
#endif

using System;
using System.Collections.Generic;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.Lean
{
    public struct LeanSveltoTask<TTask>: ISveltoTask where TTask : IEnumerator<TaskContract>
    {
        internal ContinuationEnumerator Run<TRunner>(TRunner   runner,
                                                       ref TTask task/*,
                                                       bool      immediate*/)
            where TRunner : class, IRunner<LeanSveltoTask<TTask>>
        {
            _sveltoTask = new SveltoTaskWrapper<TTask, IRunner<LeanSveltoTask<TTask>>>(ref task, runner);
#if DEBUG && !PROFILER                        
            DBC.Tasks.Check.Require(IS_TASK_STRUCT == true || task != null, 
                                    "A valid enumerator is required to enable a LeanSveltTask ".FastConcat(ToString()));
   
            DBC.Tasks.Check.Require(runner != null, "SetScheduler function has never been called");
#endif            
    
            _continuationEnumerator = new ContinuationEnumerator(ContinuationPool.RetrieveFromPool());
            _threadSafeSveltoTaskStates.started = true;
            
            runner.StartCoroutine(ref this/*, immediate*/);

            return _continuationEnumerator;
        }
        
        public override string ToString()
        {
#if GENERATE_NAME
            if (_name == null)
                _name = _sveltoTask.task.ToString();
    
            return _name;
#else
            return "LeanSveltoTask";
#endif            
        }

        public void Stop()
        {
            _threadSafeSveltoTaskStates.explicitlyStopped = true;
        }

        public string name => ToString();

        TaskContract ISveltoTask.Current => throw new Exception();
        TTask Current => _sveltoTask.task;

        public bool MoveNext()
        {
            DBC.Tasks.Check.Require(_threadSafeSveltoTaskStates.completed == false, "impossible state");
            bool completed = false;
            try
            {
                if (_threadSafeSveltoTaskStates.explicitlyStopped == false)
                {
                        completed = !_sveltoTask.MoveNext();
                }
                else
                    completed = true;
            }
            finally
            {
                if (completed == true)
                {
                    _continuationEnumerator.ce.ReturnToPool();
                    _threadSafeSveltoTaskStates.completed = true;
                }                
            }

            return !completed;
        }

        SveltoTaskWrapper<TTask, IRunner<LeanSveltoTask<TTask>>> _sveltoTask;
        SveltoTaskState                                      _threadSafeSveltoTaskStates;
        ContinuationEnumerator                               _continuationEnumerator;
#if GENERATE_NAME
        string _name;
#endif
#if DEBUG && !PROFILER
        static readonly bool IS_TASK_STRUCT = typeof(TTask).IsValueType;
#endif

    }
}