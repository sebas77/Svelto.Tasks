using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Svelto.DataStructures;

namespace Svelto.Tasks
{
    public interface ITaskCollection<T> : IEnumerator<TaskCollection<T>.CollectionTask>, IEnumerator<T>
        where T : IEnumerator
    {
        event Action                onComplete;
        event Func<Exception, bool> onException;
        
        void Add(T enumerator);
        void Clear();
        
        bool isRunning { get; }
    }

    public abstract partial class TaskCollection<T>:ITaskCollection<T> where T:IEnumerator
    {
        public event Action                onComplete;
        public event Func<Exception, bool> onException;
        
        public bool  isRunning { private set; get; }

        protected TaskCollection(int initialSize)
        {
            _currentTask = new CollectionTask(this);
            
            _listOfStacks = new FasterList<StructFriendlyStack>(initialSize);
            for (int i = 0; i < initialSize; i++)
                _listOfStacks[i] = new StructFriendlyStack(1);
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

                throw;
            }
            
            isRunning = false;

            return false;
        }

        public void Add(T enumerator)
        {
            var buffer = _listOfStacks.ToArrayFast();
            var count = _listOfStacks.Count;
            if (count < buffer.Length && buffer[count].isValid())
            {
                buffer[count].Clear();
                buffer[count].Push(enumerator);
                _listOfStacks.AddRef(ref buffer[count]);
            }
            else
            {
                var stack = new StructFriendlyStack(_INITIAL_STACK_SIZE);
                stack.Push(enumerator);
                _listOfStacks.Add(stack);
            }
        }
        
        object IEnumerator.Current
        {
            get { return Current.current; }
        }

        public CollectionTask Current
        {
            get { return _currentTask;  }
        }

        /// <summary>
        /// Restore the list of stacks to their original state
        /// </summary>
        void IEnumerator.Reset()
        {
            Reset();
        }

        T IEnumerator<T>.Current
        {
            get 
            { 
                int enumeratorIndex;
                var stacks = _listOfStacks[_stackOffset].Peek(out enumeratorIndex);
                return stacks[enumeratorIndex];
            }
        }
        
        public void Clear()
        {
            _listOfStacks.Clear();
         
            _stackOffset = 0;
        }

        public void Reset()
        {
            var count = _listOfStacks.Count;
            for (int index = 0; index < count; ++index)
            {
                var stack = _listOfStacks[index];
                while (stack.count > 1) stack.Pop();
                int stackIndex;
                stack.Peek(out stackIndex)[stackIndex].Reset();
            }

            _stackOffset = 0;
        }

        protected abstract bool RunTasksAndCheckIfDone();

        protected TaskState ProcessStackAndCheckIfDone()
        {
            int enumeratorIndex;
            var listBuffer = _listOfStacks.ToArrayFast();
            var stack = listBuffer[_stackOffset].Peek(out enumeratorIndex);

            ProcessTask(ref stack[enumeratorIndex]);
                
            bool isDone  = !stack[enumeratorIndex].MoveNext();
            
            //Svelto.Tasks Tasks IEnumerator are always IEnumerator returning an object
            //so Current is always an object
            var returnObject = _currentTask.current = stack[enumeratorIndex].Current;

            if (isDone == true)
            {
                var disposable = stack[enumeratorIndex] as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
                
                return TaskState.doneIt;
            }

            //can be a Svelto.Tasks Break
            if (returnObject == Break.It || returnObject == Break.AndStop)
            {
                _currentTask.breakIt = returnObject as Break;

                return TaskState.breakIt;
            }
            
            //can be a frame yield
            if (returnObject == null)
                return TaskState.yieldIt;

#if DEBUG && !PROFILER                
            if (returnObject is IAsyncTask)
                throw new ArgumentException("Svelto.Task 2.0 is not supporting IAsyncTask implicitly anymore, use AsyncTaskWrapper instead " + ToString()); 

            if (returnObject is TaskRoutine<T>)
                throw new ArgumentException("Returned a TaskRoutine without calling Start first " + ToString());
#endif            
            //can be a compatible IEnumerator  
            if (returnObject is T)
                listBuffer[_stackOffset].Push((T)returnObject); //push the new yielded task and execute it immediately
            
            return TaskState.continueIt;
        }
        
        protected int taskCount { get { return _listOfStacks.Count(); }}
        protected StructFriendlyStack[] rawListOfStacks { get { return _listOfStacks.ToArrayFast(); } }
        protected abstract void ProcessTask(ref T Task);

        protected int  _stackOffset;
        CollectionTask _currentTask;
        readonly FasterList<StructFriendlyStack> _listOfStacks;

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

            public CollectionTask(TaskCollection<T> parent):this()
            {
                _parent = parent;
            }

            public void Add(T task)
            {
                _parent.Add(task);
            }

            readonly TaskCollection<T> _parent;
            public Break breakIt { internal set; get; }
        }
    }
}



