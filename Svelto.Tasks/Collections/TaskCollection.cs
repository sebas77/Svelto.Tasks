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
        // event Action                onComplete;
        // event Func<Exception, bool> onException;
        //
        // ref T CurrentStack { get; }
        //
        // void Add(in T enumerator);
        // void Clear();
        //
        // bool isRunning { get; }
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
        {}

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

        //todo unit test this
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
        //todo unit test this
        public void Reset()
        {
            isRunning = false;
            
            var count = _listOfStacks.count;
            for (int index = 0; index < count; ++index)
            {
                var stack = _listOfStacks[index];
                while (stack.count > 1) stack.Pop();
                stack.Peek().Reset(); 
            }

            _currentStackIndex = 0;
        }
        
        public void Clear()
        {
            isRunning = false;
            
            var stacks = _listOfStacks.ToArrayFast(out _);
            var count  = _listOfStacks.count;
            
            for (int index = 0; index < count; ++index)
                stacks[index].Clear();
            
            _listOfStacks.FastClear();
         
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
            _currentStackIndex = currentindex;
            var listOfStacks = _listOfStacks.ToArrayFast(out _);
            ref var enumerator = ref listOfStacks[_currentStackIndex].Peek();

            bool isDone  = !enumerator.MoveNext();
            
            if (isDone == true)
                return TaskState.doneIt;

            if (enumerator is T taskContractEn)
            {
                //Svelto.Tasks Tasks IEnumerator are always IEnumerator returning an object so Current is always an object
                //can yield for one iteration
                if (taskContractEn.Current.yieldIt)
                    return TaskState.yieldIt;

                //can be a Svelto.Tasks Break
                if (taskContractEn.Current.breakIt == Break.It || taskContractEn.Current.breakIt == Break.AndStop)
                    return TaskState.breakIt;

                    //careful it must be the array and not the list as it returns a struct!!
                 listOfStacks[_currentStackIndex].Push(taskContractEn); //push the new yielded task and execute it immediately
            }

            return TaskState.continueIt;
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
        
        //TaskContract                             _currentTask; reinsert if we want to use IEnumerator for taskcollection
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



