using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Tasks
{
    /// <summary>
    /// Todo: this cannot be used at the moment because TaskCollection can handle only T parameters, that
    /// are specific type of IEnumerator<TaskContract>. This means that it cannot push on the stack a normal
    /// IEnumerator, that is a necessary option in case an IEnumerator<TaskContract> returns an IEnumerator
    /// This is solved differently in the runners, because TaskContract can hold both kind of IEnumerators.
    /// Of course IEnumerators can be returned only by Iterator blocks that are also IEnumerators so the problem
    /// could be solved if we decide
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ITaskCollection<T> : IEnumerator<TaskContract>
        where T : IEnumerator
    {
    }

    public abstract partial class TaskCollection<T>:ITaskCollection<T>
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
            
            isRunning = false;

            return false;
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
         
            _currentStackIndex = 0;
        }

        public ref T CurrentStack => ref _listOfStacks[_currentStackIndex].Peek();

        public TaskContract Current
        {
            get
            {
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

                //can be a Svelto.Tasks Break
                if (contract.breakMode == TaskContract.Break.It || contract.breakMode == TaskContract.Break.AndStop)
                    return TaskState.breakIt;

                if (contract.isTaskEnumerator(out var t))
                {
                    if (t.enumerator is T casted)
                    {
                        arrayOfTasks[_currentStackIndex].Push(casted);
                        return TaskState.continueIt;
                    }

                    Console.LogError($"TaskCollection: enumerator is not of type of {typeof(T)}");
                }
                
                if (contract.isExtraLeanEnumerator(out IEnumerator extraEnum))
                {
                    return StepExtraEnumerator(extraEnum);
                }
            }

            return TaskState.continueIt;
        }
        
        TaskState StepExtraEnumerator(IEnumerator extraEnumerator)
        {
            // run one step
            if (extraEnumerator.MoveNext() == false)
                return TaskState.continueIt;          // child finished, keep running parent

            // interpret the value it yielded (can be null or TaskContract)

            if (extraEnumerator.Current is not TaskContract yielded || yielded.yieldIt)
                return TaskState.yieldIt;             // wait until next frame

            if (yielded.breakMode == TaskContract.Break.AndStop)
                return TaskState.breakIt;             // propagate hard break

            if (yielded.breakMode == TaskContract.Break.It)
                return TaskState.continueIt;          // soft break – resume parent next loop

            // Anything else is illegal for a plain IEnumerator
            throw new SveltoTaskException(
                $"Extra-lean enumerator {extraEnumerator} can only yield null, Yield.It, Break.It or Break.AndStop");
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



