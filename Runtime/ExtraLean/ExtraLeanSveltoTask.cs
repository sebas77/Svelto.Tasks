#if ENABLE_PLATFORM_PROFILER || TASKS_PROFILER_ENABLED || (DEBUG && !PROFILER)
#define GENERATE_NAME
#endif

using System.Collections;
using DBC.Tasks;

namespace Svelto.Tasks.ExtraLean
{
    public struct ExtraLeanSveltoTask<TTask> : ISveltoTask where TTask : IEnumerator
    {
        internal void Run<TRunner>(TRunner runner, ref TTask task/* , bool immediate*/)
            where TRunner : class, IRunner<ExtraLeanSveltoTask<TTask>>
        {
            _runningTask  = task;
            
#if DEBUG && !PROFILER
            DBC.Tasks.Check.Require(IS_TASK_STRUCT == true || task != null, 
                "A valid enumerator is required to enable an ExtraLeanSveltTask ".FastConcat(ToString()));
            Check.Require(runner != null, "The runner cannot be null ".FastConcat(ToString()));
#endif
            
            _threadSafeSveltoTaskStates.started = true;

            runner.StartCoroutine(ref this/*, immediate*/);
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

        public string name => ToString();

        public TaskContract Current => Yield.It;

        /// <summary>
        ///     Move Next is called by the current runner, which could be on another thread! that means that the
        ///     --->class states used in this function must be thread safe<-----
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            Check.Require(_threadSafeSveltoTaskStates.completed == false, "ExtraLeanSveltoTask impossible state ");

            bool completed;
            if (_threadSafeSveltoTaskStates.explicitlyStopped == false)
                completed = !_runningTask.MoveNext();
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
        string _name;
#endif
#if DEBUG && !PROFILER
        static readonly bool IS_TASK_STRUCT = typeof(TTask).IsValueType;
#endif
    }
}