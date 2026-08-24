using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.DataStructures;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.Lean
{
    struct SveltoTaskWrapper<TTask, TRunner>
            where TTask : IEnumerator<TaskContract> where TRunner : class, IRunner<LeanSveltoTask<TTask>>
    {
        internal SveltoTaskWrapper(in TTask task, TRunner runner) : this()
        {
            _runner   = runner;
            this.task = task;
        }

        internal string name => task.ToString();
        
        internal void Dispose()
        {
            try
            {
                if (EqualityComparer<TTask>.Default.Equals(task, default) == false)
                    task.Dispose();
            }
            catch (NotImplementedException )
            {
            }

            _continuingTask = default;
            _current = default;
        }

        internal StepState Step(int runningTaskIndexFromRunningTasksToReplace, TombstoneHandle parentSpawnedTaskIndex)
        {
            //if the tasks returned an extraLeanEnumerator, the parent task takes responsibility to run it. This is because extraLeanEnumerator
            //can run only on extra lean runners, while lean tasks runs only on lean runners.
            if (_current.isExtraLeanEnumerator(out IEnumerator extraLeanEnumerator) == true)
            {
                var state = ProcessExtraLeanEnumerator(extraLeanEnumerator, ref _current);
                if (state != StepState.Invalid)
                    return state;
            }
            else
            //This task cannot continue until the spawned task is not finished.
            //"continuation" different from null signals that a spawned task is still running so this task cannot continue
            //continuation is != null both in the RunOn case (continuation returned directly) and in the Continue case (continuation generated
            //by this wrapper)
            if (_current.continuation != null)
            {
                if (_current.continuation.Value.isRunning == true)
                    return StepState.Running; //even if a child task replaces the parent task, a task can return directly a continuation (think about RunOn), in that case we can't do anything else than spinning 
                
                //if isContinued == true a Continue() task has been yielded
                //if isContinued == false a RunOn() task has been yielded 
                //Break And Stop works only with .Continue() and not with .RunOn() 
                //if I want to do the same on a RunOn() task I have to let the Continuation know about the runner and the ID of the task in the runner
                //to be able to query the enumerator current object, in that case _continuingTask will be queried by the continuation
                if (_current.isContinued) //the task just completed is a Continue() task, we have some extra info about it
                {
                    var currentBreakMode = _continuingTask.Current.breakMode;

                    _current = default; //the parent task must return the child task result. this value will be returned if the task completes next step
                    _continuingTask = default; //finish waiting for the continuator, reset it

                    //the child task is telling to interrupt everything! No need to do the next move next
                    if (currentBreakMode == TaskContract.Break.AndStop)
                        return StepState.Completed;
                }
            }

            //child task is completed, continue the normal execution of this task
            try
            {
                bool result;
                while ((result = task.MoveNext()) == true && task.Current.continueIt) ;

                if (result == false)
                {
                    task.Dispose();
                    return StepState.Completed;
                }
            }
            catch (Exception e)
            {
                Console.LogException(e);
                
                throw;
            }

            _current = task.Current;
#if DEBUG && !PROFILE_SVELTO
            DBC.Tasks.Check.Assert(_current.continuation?._runner != _runner,
                $"Cannot yield a new task running on the same runner of the spawning task, use Continue() instead {_current}");
#endif
            if (_current.yieldIt)
                return StepState.Running;

            //hasValue stops the execution early, to Unit Test. It seems to be necessary too!
            if (_current.breakMode == TaskContract.Break.It || _current.breakMode == TaskContract.Break.AndStop || _current.hasValue)
            {
                task.Dispose();
                return StepState.Completed;
            }

            //this exists to run IEnumerator that are set to run immediately!
            if (_current.isExtraLeanEnumerator(out var extraLeanEnumerator1))
            {
                var state = ProcessExtraLeanEnumerator(extraLeanEnumerator1, ref _current);
                if (state != StepState.Invalid)
                    return state;
            }
            else
            //this means that the previous MoveNext returned an enumerator continued with .Continue().
            //Instead a RunOn() directly generates a continuation and doesn't pass through this if 
            //as _current.continuation is set instead. Continue() must be resolved in this way and not like RunOn() because
            //the runner to continue the task on is known only at this point.
            if (_current.isTaskEnumerator(out (IEnumerator<TaskContract> enumerator, bool isFireAndForget) tuple) == true)
            {
                //Handle the Continue() case, the new task must "continue" using the current runner
                //the current task will continue waiting for the new spawned task through the continuation

                //a new TaskContract is created, holding the continuationEnumerator of the new task
                //it must be added in the runner as "spawned" task and must run separately from this task
                var tupleEnumerator = tuple.enumerator;
                
                DBC.Tasks.Check.Assert(tupleEnumerator != null);
                
                LeanSveltoTask<TTask> leanSveltoTask = default;

                //.Forget() case, a special case of .Continue()
                if (tuple.isFireAndForget == true)
                    leanSveltoTask.Run(_runner, (TTask)tupleEnumerator);
                else
                {
#if DEBUG && !PROFILE_SVELTO
                    var continuation = new Continuation(ContinuationPool.RetrieveFromPool(), _runner);
#else
                     var continuation = new Continuation(ContinuationPool.RetrieveFromPool());
#endif
                    //note: this is a struct and this must be completely set before calling SpawnContinuingTask
                    //as it can trigger a resize of the datastructure that contains this, invalidating this
                    //TestThatLeanTasksWaitForContinuesWhenRunnerListsResize unit test covers this case
                    _current = new TaskContract(continuation, true);
                    _continuingTask = tupleEnumerator; //remember the child task

                    leanSveltoTask.SpawnContinuingTask(_runner, (TTask)tupleEnumerator, continuation, (runningTaskIndexFromRunningTasksToReplace, parentSpawnedTaskIndex));
                }
            }

            return StepState.Running;

            static StepState ProcessExtraLeanEnumerator(IEnumerator extraLeanEnumerator, ref TaskContract current)
            {
                StepState state = StepState.Invalid;
                //if the returned enumerator is NOT a taskcontract one, the continuing task cannot spawn new tasks,
                //so we can simply iterate it here until is done. This MUST run instead of the normal task.MoveNext()
                try
                {
                    if (extraLeanEnumerator.MoveNext() == false)
                    {
                        current = default; //extra lean enumerator is done, reset the current task to null to signal the parent task (this object) to continue next step (basically the isExtraLeanEnumerator will return false next time)
                        DisposeEnumerator(extraLeanEnumerator);
                    }
                    else
                    {
                        var extraLeanChildTaskCurrent = extraLeanEnumerator.Current;

                        if (extraLeanChildTaskCurrent == TaskContract.Yield.It)
                            state = StepState.Running; //this task is not waiting, is running the child task
                        else
                        if (extraLeanChildTaskCurrent == TaskContract.Break.AndStop)
                            state = StepState.Completed;
                        else
                        if (extraLeanChildTaskCurrent == TaskContract.Break.It)
                            current = default; //reset the current task to null to signal the parent task to continue next step
                        else
                            throw new SveltoTaskException(
                                $"ExtraLean enumerator {extraLeanEnumerator} can return only null, Yield.It, Break.It, Break.AndStop and yield break");
                    }
                }
                catch (Exception e)
                {
                    Console.LogException(e);

                    throw;
                }

                DBC.Tasks.Check.Assert(current.continuation.Equals(default));

                return state;
                
                static void DisposeEnumerator(in IEnumerator task)
                {
                    if (task is IDisposable disposable)
                        disposable.Dispose(); //dispose the enumerator, it won't be used anymore
                }
            }
        }

        TTask task { get; } //current task to wrap
        
        //Todo would be much better to hold an index to the task in the runner to save memory 
        TaskContract _current; //if the task is waiting for a continuation (Continue or RunOn), this will hold the continuation
 
        //todo optimization: it's important to get rid of these fields in one way or another. Best would be to store them
        //in the continuation class (not struct)
        IEnumerator<TaskContract> _continuingTask; //if the task is waiting for a Continue() case, this will hold the task continued
        readonly TRunner _runner; //runner that is running this task
    }
}