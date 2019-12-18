using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Tasks
{
    public interface ITaskCollection<T> : IEnumerator<TaskCollection<T>.CollectionTask>
        where T : IEnumerator
    {
        event Action                onComplete;
        event Func<Exception, bool> onException;
        
        T CurrentStack { get; }
        
        void Add(T enumerator);
        void Clear();
        
        bool isRunning { get; }
    }

    public abstract partial class TaskCollection<T>:ITaskCollection<T> where T:IEnumerator
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
            _currentTask = new CollectionTask(this);
            
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

        public void Add(T enumerator)
        {
            DBC.Tasks.Check.Require(isRunning == false, "can't modify a task collection while its running");
            
            var buffer = _listOfStacks.ToArrayFast();
            var count = _listOfStacks.Count;
            if (count < buffer.Length && buffer[count].isValid())
            {
                buffer[count].Clear();
                buffer[count].Push(enumerator);
                
                _listOfStacks.ReuseOneSlot();
            }
            else
            {
                var stack = new StructFriendlyStack(_INITIAL_STACK_SIZE);
                _listOfStacks.Add(stack);
                buffer = _listOfStacks.ToArrayFast();
                buffer[_listOfStacks.Count - 1].Push(enumerator);
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
                return stacks[ enumeratorIndex];
            }
        }

        public CollectionTask Current
        {
            get { return _currentTask;  }
        }

        object IEnumerator.Current
        {
            get { return CurrentStack; }
        }

        public void Clear()
        {
            isRunning = false;
            
            var buffer = _listOfStacks.ToArrayFast();
            var count = _listOfStacks.Count;
            
            for (int index = 0; index < count; ++index)
                buffer[index].Clear();
            
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
            var returnObject = _currentTask.current = stack[enumeratorIndex].Current;

            if (isDone == true)
                return TaskState.doneIt;

            //can be a Svelto.Tasks Break
            if (returnObject == Break.It || returnObject == Break.AndStop)
            {
                _currentTask.breakIt = returnObject as Break;

                return TaskState.breakIt;
            }
            
            _currentTask.breakIt = null;
            
            //can yield for one iteration
            if (returnObject == null) 
                return TaskState.yieldIt;

#if DEBUG && !PROFILER                
            if (returnObject is IServiceTask)
                throw new ArgumentException("Svelto.Task 2.0 is not supporting IAsyncTask implicitly anymore, use TaskServiceEnumerator instead " + ToString()); 

            if (returnObject is ITaskRoutine<IEnumerator>)
                throw new ArgumentException("Returned a TaskRoutine without calling Start first " + ToString());
#endif            
              
            if (returnObject is T) //can be a compatible IEnumerator
            //careful it must be the array and not the list as it returns a struct!!
                listOfStacks[_currentStackIndex].Push((T)returnObject); //push the new yielded task and execute it immediately
            
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
        
        CollectionTask                           _currentTask;
        int                                      _currentStackIndex;
        readonly FasterList<StructFriendlyStack> _listOfStacks;
        T                                        _current;
        string                                   _name;

        const int _INITIAL_STACK_SIZE = 1;
        
        protected enum TaskState
        {
            doneIt,
            breakIt,
            continueIt,
            yieldIt
        }

        public struct CollectionTask
        {
            public object current { get; internal set; }

            public CollectionTask(TaskCollection<T> collection):this()
            {
                _collection = collection;
            }

            public void Add(T task)
            {
                _collection.Add(task);
            }

            public T Current
            {
                get { return _collection.CurrentStack;  }
            }

            readonly TaskCollection<T> _collection;
            public Break breakIt { internal set; get; }
        }
    }
}



