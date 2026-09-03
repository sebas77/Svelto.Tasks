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
            _runner = runner;
            _task   = task;
        }

        //TypeCache computes the name once per closed TTask type: _task.ToString() on every
        //profiler step would allocate (compiler-generated iterator ToString builds the type name)
        internal string name
        {
            get
            {
                #if !PROFILE_SVELTO
                return _task.ToString();
                #else
                return Common.TypeCache<TTask>.name;
#endif
            }
        }

        internal void Dispose()
        {
            try
            {
                if (EqualityComparer<TTask>.Default.Equals(_task, default) == false)
                    _task.Dispose();
            }
            catch (NotImplementedException )
            {
            }

            //a pending inline ExtraLean child is owned by this wrapper: release it on teardown
            //(Break.AndStop, Stop, Flush/Dispose, faults). Break.It and natural completion already
            //cleared _current, so the alive-by-contract and completed cases can't be reached here.
            if (_current.isExtraLeanEnumerator(out IEnumerator pendingChild) == true &&
                pendingChild is IDisposable disposable)
                disposable.Dispose();

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
                //Break.AndStop only applies to a .Continue() chain. A .RunOn() task belongs to a separate runner path.
                if (_current.isContinued) //the task just completed is a Continue() task, we have some extra info about it
                {
                    var currentBreakMode = _continuingTask.Current.breakMode;

                    _current = default; //the parent task must return the child task result. this value will be returned if the task completes next step
                    _continuingTask = default; //finish waiting for the continuator, reset it

                    //The runner normally catches this on the child. Keep the signal for paths that complete here.
                    if (currentBreakMode == TaskContract.Break.AndStop)
                        return StepState.Completed | StepState.StopParentChain;
                }
            }

            //child task is completed, continue the normal execution of this task
            //exceptions are intentionally not handled here: the runner routes task faults through
            //TaskExceptionStrategy, keeping a single reporting source
            bool result;
            while ((result = _task.MoveNext()) == true && _task.Current.continueIt) ;

            if (result == false)
                return StepState.Completed;

            _current = _task.Current;
#if DEBUG && !PROFILE_SVELTO
            DBC.Tasks.Check.Assert(_current.continuation?._runner != _runner,
                $"Cannot yield a new task running on the same runner of the spawning task, use Continue() instead {_current}");
#endif
            if (_current.yieldIt)
                return StepState.Running;

            if (_current.breakMode == TaskContract.Break.AndStop)
                return StepState.Completed | StepState.StopParentChain;

            //hasValue stops the execution early, to Unit Test. It seems to be necessary too!
            if (_current.breakMode == TaskContract.Break.It || _current.hasValue)
                return StepState.Completed;

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
                //so we can simply iterate it here until is done. This MUST run instead of the normal _task.MoveNext()
                //exceptions are intentionally not handled here: the runner routes task faults through
                //TaskExceptionStrategy, keeping a single reporting source
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
                        state = StepState.Completed | StepState.StopParentChain;
                    else
                    if (extraLeanChildTaskCurrent == TaskContract.Break.It)
                        current = default; //reset the current task to null to signal the parent task to continue next step
                    else
                        throw new SveltoTaskException(
                            $"ExtraLean enumerator {extraLeanEnumerator} can return only null, Yield.It, Break.It, Break.AndStop and yield break");
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

        //The task must be stored as a plain field and consumed through a reference to the wrapper:
        //a get-only property (or a readonly view) hands out a copy of a struct TTask, so MoveNext
        //would mutate the copy and struct tasks would never progress (this is why generic runners
        //could not run struct tasks before). A ref-returning property is not an alternative because
        //this wrapper is a struct itself and structs cannot return their fields by reference (CS8170).
        //The scheduler reaches the wrapper by ref, so __task.MoveNext() mutates it in place inside the
        //TombstoneList slot, exactly like ExtraLeanSveltoTask does with _runningTask.
        TTask _task; //current task to wrap
        
        //Task and continuation-state indirection is deferred; see
        //PROPOSALS/001-TaskStore-handle-indirection.md.
        TaskContract _current; //if the task is waiting for a continuation (Continue or RunOn), this will hold the continuation

        IEnumerator<TaskContract> _continuingTask; //if the task is waiting for a Continue() case, this will hold the task continued
        readonly TRunner _runner; //runner that is running this task
    }
}

