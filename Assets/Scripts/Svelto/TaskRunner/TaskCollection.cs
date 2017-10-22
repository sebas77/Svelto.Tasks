using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Tasks
{
    abstract public class TaskCollection: IEnumerator
    {
        public bool            isRunning { protected set; get; }
        public abstract object Current   { get; }

        public abstract bool MoveNext();
        public abstract void Reset();

        public void Clear()
        {
            _listOfStacks.Clear();
        }

        public TaskCollection Add(ITask task)
        {
            if (task == null)
                throw new ArgumentNullException();

            Add(new TaskWrapper(task));

            return this;
        }

        public TaskCollection Add(IEnumerator enumerator)
        {
            if (enumerator == null)
                throw new ArgumentNullException();

            CheckForToken(enumerator);

            Stack<IEnumerator> stack;
            if (_listOfStacks.Reuse(_listOfStacks.Count, out stack) == false)
                stack = new Stack<IEnumerator>(_INITIAL_STACK_SIZE);
            else
                stack.Clear();

            stack.Push(enumerator);
            _listOfStacks.Add(stack);

            return this;
        }

        /// <summary>
        /// Restore the list of stacks to their original state
        /// </summary>
        public void SafeReset()
        {
            var count = _listOfStacks.Count;
            for (int index = 0; index < count; ++index)
            {
                Stack<IEnumerator> stack = _listOfStacks[index];
                while (stack.Count > 1) stack.Pop();
            }
        }

        protected TaskCollection()
                    : this(_INITIAL_STACK_COUNT)
        { }

        protected TaskCollection(int initialSize)
        {
            _listOfStacks = FasterList<Stack<IEnumerator>>.PreFill<Stack<IEnumerator>>(initialSize);
        }

        protected IEnumerator StandardEnumeratorCheck(object current)
        {
            var enumerator = current as IEnumerator;
            if (enumerator != null)
            {
                CheckForToken(current);

                return enumerator;
            }

            ///
            /// Careful an array is an IEnumerable!!!
            /// 
            var ptasks = current as IEnumerator[]; 
            if (ptasks != null)
                return new ParallelTaskCollection(ptasks);

            var task = current as IAbstractTask;
            if (task != null)
                return CreateTaskWrapper(task);
            
            var enumerable = current as IEnumerable;
            if (enumerable != null)
                throw new TaskYieldsIEnumerableException("Yield an IEnumerable is not supported " + current.GetType());

            return null;
        }

        protected virtual TaskWrapper CreateTaskWrapper(IAbstractTask task)
        {
            var taskI = task as ITask;

            if (taskI == null)
                throw new ArgumentException();

            return new TaskWrapper(taskI);
        }

        protected virtual void CheckForToken(object current)
        {}       

        protected FasterList<Stack<IEnumerator>> _listOfStacks;

        const int _INITIAL_STACK_COUNT = 3;
        const int _INITIAL_STACK_SIZE = 3;
    }
}

