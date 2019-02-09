using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Tasks
{
    public interface ITaskCollection<T> : IEnumerator<TaskContract>
        where T : IEnumerator
    {
        event Action                onComplete;
        event Func<Exception, bool> onException;
        
        T CurrentStack { get; }
        
        void Add(ref T enumerator);
        void Clear();
        
        bool isRunning { get; }
    }

    public abstract partial class TaskCollection<T>:ITaskCollection<T>
       where T:IEnumerator<TaskContract> //eventually this could go back to IEnumerator if makes sense
    {
        public event Action                onComplete;
        public event Func<Exception, bool> onException;
        
        public bool  isRunning { private set; get; }
        
        protected TaskCollection(int initialStackCount): this(String.Empty, initialStackCount)
        {
            _name = base.ToString();
        }

        protected TaskCollection(string name, int initialSize)
        {
            _name = name;
            _listOfStacks = new FasterList<StructFriendlyStack>(initialSize);
            var buffer = _listOfStacks.ToArrayFast();
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
                
                if (onComplete != null)
                    onComplete();
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

        public void Add(ref T enumerator)
        {
            DBC.Tasks.Check.Require(isRunning == false, "can't modify a task collection while its running");
            
            var buffer = _listOfStacks.ToArrayFast();
            var count = _listOfStacks.Count;
            if (count < buffer.Length && buffer[count].isValid())
            {
                buffer[count].Clear();
                buffer[count].Push(ref enumerator);
                
                _listOfStacks.ReuseOneSlot();
            }
            else
            {
                var stack = new StructFriendlyStack(_INITIAL_STACK_SIZE);
                _listOfStacks.AddRef(ref stack);
                buffer = _listOfStacks.ToArrayFast();
                buffer[_listOfStacks.Count - 1].Push(ref enumerator);
            }
        }
        
        /// <summary>
        /// Restore the list of stacks to their original state
        /// </summary>
        public void Reset()
        {
            isRunning = false;
            
            var count = _listOfStacks.Count;
            for (int index = 0; index < count; ++index)
            {
                var stack = _listOfStacks[index];
                while (stack.count > 1) stack.Pop();
                int stackIndex;
                stack.Peek(out stackIndex)[stackIndex].Reset(); 
            }

            _currentStackIndex = 0;
        }

        public T CurrentStack
        {
            get
            {
                    int enumeratorIndex;
                    var stacks = _listOfStacks[_currentStackIndex].Peek(out enumeratorIndex);
                    return stacks[enumeratorIndex];
            }
        }

        public TaskContract Current
        {
            get
            {
                if (_listOfStacks.Count > 0)
                    return CurrentStack.Current;
                else
                    return new TaskContract();
            }
        }

        object IEnumerator.Current
        {
            get { return CurrentStack; }
        }

        public void Clear()
        {
            isRunning = false;
            
            var stacks = _listOfStacks.ToArrayFast();
            var count = _listOfStacks.Count;
            
            for (int index = 0; index < count; ++index)
                stacks[index].Clear();
            
            _listOfStacks.FastClear();
         
            _currentStackIndex = 0;
        }

        protected TaskState ProcessStackAndCheckIfDone(int currentindex)
        {
            _currentStackIndex = currentindex;
            int enumeratorIndex;
            var listOfStacks = _listOfStacks.ToArrayFast();
            var stack = listOfStacks[_currentStackIndex].Peek(out enumeratorIndex);

            ProcessTask(ref stack[enumeratorIndex]);
                
            bool isDone  = !stack[enumeratorIndex].MoveNext();
            
            //Svelto.Tasks Tasks IEnumerator are always IEnumerator returning an object so Current is always an object
            var returnObject = stack[enumeratorIndex].Current;

            if (isDone == true)
                return TaskState.doneIt;
            
            //can yield for one iteration
            if (returnObject.yieldIt) 
                return TaskState.yieldIt;

            //can be a Svelto.Tasks Break
            if (returnObject.breakit == Break.It || returnObject.breakit == Break.AndStop)
                return TaskState.breakIt;

            if (returnObject.enumerator is T) //can be a compatible IEnumerator
            //careful it must be the array and not the list as it returns a struct!!
            {
                var valueEnumerator = (T)returnObject.enumerator;
                listOfStacks[_currentStackIndex].Push(ref valueEnumerator); //push the new yielded task and execute it immediately
            }

            return TaskState.continueIt;
        }

        public override string ToString()
        {
            if (_name == null)
                _name = base.ToString(); 

            return _name;
        }
        
        protected int taskCount { get { return _listOfStacks.Count; }}
        protected StructFriendlyStack[] rawListOfStacks { get { return _listOfStacks.ToArrayFast(); } }

        protected abstract void ProcessTask(ref T Task);
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



