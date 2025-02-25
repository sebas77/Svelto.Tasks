using System.Collections.Generic;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.Lean
{
    internal struct SveltoTaskWrapper<TTask, TRunner>
        where TTask : IEnumerator<TaskContract> where TRunner : class, IRunner<LeanSveltoTask<TTask>>
    {
        public SveltoTaskWrapper(in TTask task, TRunner runner) : this()
        {
            _runner   = runner;
            this.task = task;
        }

        public bool MoveNext()
        {
            //This task cannot continue until the spawned task is not finished.
            //"continuation" signals that a spawned task is still running so this task cannot continue
            if (_current.continuation != null)
            {
                //a task is waiting to be completed, spin this one
                if (_current.continuation.Value.isRunning)
                    return true;

                //if _continuingTask != null Continue() has been yielded
                //if _continuingTask == null RunOn() has been yielded
                if (_continuingTask != null)
                {
                    //the child task is telling to interrupt everything!
                    var currentBreakIt = _continuingTask.Current.breakIt;
                    _current = new TaskContract(); //finish to wait for the continuator, reset it
                    
                    _continuingTask = null;

                    if (currentBreakIt == Break.AndStop)
                        return false;
                }
            }
            

            //this means that the previous MoveNext returned an enumerator, it may be a continuation case
            if (_current.isExtraLeanEnumerator(out var extraLeanEnumerator) == true)
            {
                //if the returned enumerator is NOT a taskcontract one, the continuing task cannot spawn new tasks,
                //so we can simply iterate it here until is done. This MUST run instead of the normal task.MoveNext()
                if (extraLeanEnumerator.MoveNext())
                    return true;

                DBC.Tasks.Check.Assert(_current.continuation.Equals(default));
            }

            //continue the normal execution of this task
            if (task.MoveNext() == false)
                return false;

            _current = task.Current;
#if DEBUG && !PROFILE_SVELTO
            DBC.Tasks.Check.Ensure(_current.continuation?._runner != _runner,
                $"Cannot yield a new task running on the same runner of the spawning task, use Continue() instead {_current}");
#endif
            if (_current.yieldIt)
                return true;

            if (_current.breakIt == Break.It || _current.breakIt == Break.AndStop || _current.hasValue)
                return false;

            //this means that the previous MoveNext returned an enumerator, it may be a continuation case
            if (_current.isTaskEnumerator(out var leanEnumerator) == true)
            {
                //Handle the Continue() case, the new task must "continue" using the current runner
                //the current task will continue waiting for the new spawned task through the continuation

                //a new TaskContract is created, holding the continuationEnumerator of the new task
                //it must be added in the runner as "spawned" task and must run separately from this task
                DBC.Tasks.Check.Require(leanEnumerator != null);

#if DEBUG && !PROFILE_SVELTO
                var continuation = new Continuation(ContinuationPool.RetrieveFromPool(), _runner);
#else
                var  continuation = new Continuation(ContinuationPool.RetrieveFromPool());
#endif
                //note: this is a struct and this must be completely set before calling SpawnContinuingTask
                //as it can trigger a resize of the datastructure that contains this, invalidating this
                //TestThatLeanTasksWaitForContinuesWhenRunnerListsResize unit test covers this case
                _current        = new TaskContract(continuation);
                _continuingTask = leanEnumerator;

                new LeanSveltoTask<TTask>().SpawnContinuingTask(_runner, (TTask)leanEnumerator, continuation);
            }

            return true;
        }

        internal TTask task { get; }

        TaskContract              _current;
        IEnumerator<TaskContract> _continuingTask;

        readonly TRunner _runner;
    }
}