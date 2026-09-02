using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Tasks
{
    /// <summary>
    /// Proposal 002-TaskCollection-IEnumerator-support to complete the support of IEnumerator in the TaskCollection. This is a more generic approach
    /// to support any kind of enumerator, not just IEnumerator<TaskContract>.
    /// </summary>
    /// <typeparam name="T"></typeparam>

    public abstract partial class TaskCollection<T>: IEnumerator<TaskContract>
       where T:IEnumerator<TaskContract> //eventually this could go back to IEnumerator if makes sense
    {
        public event Func<Exception, bool> onException;
        
        public bool  isRunning { private set; get; }
        
        protected TaskCollection(int initialStackCount): this(String.Empty, initialStackCount)
        {
            _name = base.ToString();
        }

        protected TaskCollection(string name, int initialSize)
        {
            _name = name;
            _listOfStacks = new FasterList<StructFriendlyStack>((uint) initialSize);
            var buffer = _listOfStacks.ToArrayFast(out _);
            for (int i = 0; i < initialSize; i++)
                buffer[i] = new StructFriendlyStack(1);
        }
        
        public void Dispose()
        {
            Clear();
        }

        public bool MoveNext()
        {
            //a hard-stopped collection stays completed until Reset()/Clear(): remaining roots are cancelled
            if (_stopped)
            {
                isRunning = false;
                return false;
            }

            _hasOverrideCurrent = false;
            isRunning = true;

            try
            {
                if (RunTasksAndCheckIfDone() == false)
                    return true;
            }
            catch (Exception e)
            {
                if (onException != null)
                {
                    var mustComplete = onException(e);

                    if (mustComplete)
                        isRunning = false;
                }
                else
                    isRunning = false;

                throw;
            }

            //Break.AndStop: yield the stop signal once, so a runner wrapper converts it to
            //StepState.StopParentChain and disposes the whole .Continue() chain waiting on this collection
            if (_chainStopped)
            {
                _chainStopped       = false;
                _overrideCurrent    = TaskContract.Break.AndStop;
                _hasOverrideCurrent = true;

                return true;
            }

            isRunning = false;

            return false;
        }

        protected void StopChain()
        {
            //unwind: everything still queued is cancelled, the collection yields Break.AndStop once
            _chainStopped = true;
            _stopped      = true;
        }

        public void Add(in T enumerator)
        {
            DBC.Tasks.Check.Require(isRunning == false, "can't modify a task collection while its running");
            
            var buffer = _listOfStacks.ToArrayFast(out _);
            var count = _listOfStacks.count;
            
            if (count < buffer.Length && buffer[count].isValid())
            {
                buffer[count].Clear();
                buffer[count].Push(enumerator);
                
                _listOfStacks.ReuseOneSlot<StructFriendlyStack>();
            }
            else
            {
                var stack = new StructFriendlyStack(_INITIAL_STACK_SIZE);
                _listOfStacks.Add(stack);
                buffer = _listOfStacks.ToArrayFast(out _);
                buffer[_listOfStacks.count - 1].Push(enumerator);
            }
        }
        
        /// <summary>
        /// Restore the list of stacks to their original state
        /// </summary>
        public virtual void Reset()
        {
            isRunning = false;

            var count = _listOfStacks.count;
            for (int index = 0; index < count; ++index)
            {
                var stack = _listOfStacks[index];
                //trimmed children are disposed by Pop() itself; roots are kept for reuse
                while (stack.count > 1) stack.Pop();
                try
                {
                    stack.Peek().Reset();
                }
                catch (NotSupportedException)
                {
                    // ignore – enumerator will simply restart next run
                }
            }

            ResetRunState();
            _currentStackIndex = 0;
        }
        
        //hard reset
        public void Clear()
        {
            isRunning = false;
            
            var stacks = _listOfStacks.ToArrayFast(out _);
            var count  = _listOfStacks.count;
            
            for (int index = 0; index < count; ++index)
                stacks[index].Clear();
            
            _listOfStacks.Clear();
         
            ResetRunState();
            _currentStackIndex = 0;
        }

        public ref T CurrentStack => ref _listOfStacks[_currentStackIndex].Peek();

        public TaskContract Current
        {
            get
            {
                if (_hasOverrideCurrent)
                    return _overrideCurrent;

                if (_listOfStacks.count > 0)
                    return CurrentStack.Current;
                
                return default;
            }
        }

        object IEnumerator.Current => throw new NotImplementedException();
       
        protected TaskState ProcessStackAndCheckIfDone(int currentindex)
        {
            //“Which individual stack am I executing right now?”
            _currentStackIndex = currentindex;
            StructFriendlyStack[] arrayOfTasks = rawListOfStacks;

            //a pending ExtraLean child owns this stack until it completes: the parent must not be advanced
            //while the child runs. The runner wrapper does the same, keeping the child inside its state.
            if (_pendingExtraEnumerator != null)
                return StepPendingExtraEnumerator();

            //it's the responsibility of the caller method to pop this enumerator from the stack, here we just execute
            ref var enumerator = ref arrayOfTasks[_currentStackIndex].Peek();

            bool isDone  = !enumerator.MoveNext();
            
            if (isDone == true)
                return TaskState.doneIt;

            if (enumerator is T taskContractEn)
            {
                TaskContract contract = taskContractEn.Current;  
                //Svelto.Tasks Tasks IEnumerator are always IEnumerator returning an object so Current is always an object
                //can yield for one iteration
                if (contract.yieldIt)
                    return TaskState.yieldIt;

                //a returned value completes this enumerator, exactly like a value-yield completes a Lean task
                if (contract.hasValue)
                    return TaskState.doneIt;

                //Break.AndStop is a hard stop: unwind the collection, MoveNext will yield StopParentChain
                if (contract.breakMode == TaskContract.Break.AndStop)
                    return TaskState.breakIt;

                //Break.It ends only THIS enumerator (aligned with runner semantics): nested, the caller pops it
                //and the parent resumes; at root level, only this root task is done and the collection continues
                if (contract.breakMode == TaskContract.Break.It)
                    return TaskState.doneIt;

                if (contract.isTaskEnumerator(out var t))
                {
                    if (t.isFireAndForget == true)
                        throw new SveltoTaskException(
                            $"{nameof(TaskCollection<T>)} cannot support .Forget(): a collection can only run " +
                            $"what it waits for, it cannot schedule independent tasks like a runner does");

                    if (t.enumerator is T casted)
                    {
                        arrayOfTasks[_currentStackIndex].Push(casted);
                        return TaskState.continueIt;
                    }

                    Console.LogError($"TaskCollection: enumerator is not of type of {typeof(T)}");
                }
                
                if (contract.isExtraLeanEnumerator(out IEnumerator extraEnum))
                {
                    //retain the child across ticks and step it immediately, like the runner wrapper does
                    _pendingExtraEnumerator = extraEnum;
                    return StepPendingExtraEnumerator();
                }
            }

            return TaskState.continueIt;
        }

        TaskState StepPendingExtraEnumerator()
        {
            //run one step of the pending child, never of the parent
            try
            {
                if (_pendingExtraEnumerator.MoveNext() == false)
                {
                    //child finished naturally: dispose it (like the runner does) and let the parent resume
                    ClearPendingExtraEnumerator(dispose: true);
                    return TaskState.continueIt;
                }

                var current = _pendingExtraEnumerator.Current;

                if (current == null)
                    return TaskState.yieldIt; //wait until next tick

                if (current is TaskContract yielded)
                {
                    if (yielded.yieldIt)
                        return TaskState.yieldIt;

                    if (yielded.breakMode == TaskContract.Break.AndStop)
                    {
                        //hard stop: the state machine stays alive (no dispose), the collection unwinds
                        ClearPendingExtraEnumerator(dispose: false);
                        return TaskState.breakIt;
                    }

                    if (yielded.breakMode == TaskContract.Break.It)
                    {
                        //soft break: keep the child's state machine alive for reuse (no dispose), resume the parent
                        ClearPendingExtraEnumerator(dispose: false);
                        return TaskState.continueIt;
                    }

                    throw new SveltoTaskException(
                        $"ExtraLean enumerator {_pendingExtraEnumerator} can only yield null, Yield.It, Break.It or Break.AndStop");
                }

                throw new SveltoTaskException(
                    $"ExtraLean enumerator {_pendingExtraEnumerator} can only yield null, Yield.It, Break.It or Break.AndStop");
            }
            catch (Exception e)
            {
                Console.LogException(e);

                throw;
            }
        }

        void ClearPendingExtraEnumerator(bool dispose)
        {
            var extraEnumerator = _pendingExtraEnumerator;
            _pendingExtraEnumerator = null;

            if (dispose && extraEnumerator is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception e)
                {
                    Console.LogException(e);
                }
            }
        }

        void ResetRunState()
        {
            ClearPendingExtraEnumerator(dispose: true);
            _chainStopped       = false;
            _stopped            = false;
            _hasOverrideCurrent = false;
        }

        public override string ToString()
        {
            if (_name == null)
                _name = base.ToString(); 

            return _name;
        }
        
        protected internal uint                  taskCount       => (uint) _listOfStacks.count;
        protected          StructFriendlyStack[] rawListOfStacks => _listOfStacks.ToArrayFast(out _);

        protected abstract bool RunTasksAndCheckIfDone();
        
        int                                      _currentStackIndex;
        readonly FasterList<StructFriendlyStack> _listOfStacks;
        string                                   _name;

        //ExtraLean child currently owned by the top stack: stepped instead of the parent until it completes
        IEnumerator _pendingExtraEnumerator;
        //Break.AndStop bookkeeping: the collection yields Break.AndStop once, then stays stopped until Reset/Clear
        bool         _chainStopped;
        bool         _stopped;
        TaskContract _overrideCurrent;
        bool         _hasOverrideCurrent;

        const int _INITIAL_STACK_SIZE = 1;
        
        protected enum TaskState
        {
            doneIt,
            breakIt,
            continueIt,
            yieldIt
        }
    }
}



