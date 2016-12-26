using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Tasks
{
    abstract public class TaskCollection
    {
        public bool                                 isRunning       { protected set; get; }
        
#if TO_IMPLEMENT_PROPERLY
        abstract public float progress { get; }
#endif

        protected TaskCollection()
            : this(_INITIAL_STACK_COUNT)
        {}

        protected TaskCollection(int initialSize)
        {
            _listOfStacks = FasterList<Stack<IEnumerator>>.PreFill<Stack<IEnumerator>>(initialSize);
        }

        public void Reset()
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

        public TaskCollection Add(IEnumerable enumerable)
        {
#if TO_IMPLEMENT_PROPERLY
            if (enumerable is TaskCollection)
        {
                registeredEnumerators.Enqueue(new EnumeratorWithProgress(enumerable.GetEnumerator(), 
                                                    () => (enumerable as TaskCollection).progress));
                
                if ((enumerable as TaskCollection).tasksRegistered == 0)
                    Console.WriteLine("Avoid to register zero size collections");
            }
            else
                registeredEnumerators.Enqueue(enumerable.GetEnumerator());
#endif
            if (enumerable == null)
                throw new ArgumentNullException();

            CheckForToken(enumerable);

            Add(enumerable.GetEnumerator());

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

