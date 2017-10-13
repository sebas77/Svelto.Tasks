using System;
using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks
{
    public class ParallelTaskCollection: TaskCollection, IEnumerator
    {
        public event Action		onComplete;
#if TO_IMPLEMENT_PROPERLY
        override public float progress { get { return _progress + _subprogress; } }
#endif

        public ParallelTaskCollection()
        {
            _currentWrapper = new ParallelTask(this);
        }

        public ParallelTaskCollection(int initialSize) : base(initialSize)
        {
            _currentWrapper = new ParallelTask(this);
        }

        public ParallelTaskCollection(IEnumerator[] ptasks) : this()
        {
            for (int i = 0; i < ptasks.Length; i++)
                Add(ptasks[i]);
        }

        public void Reset()
        {
            _index = 0;
        }

        public new void Clear()
        {
            base.Clear();
            _index = 0;
        }

        public bool MoveNext()
        {
            isRunning = true;
            
            if (RunTasks()) return true;
            
            isRunning = false;
            
            if (onComplete != null)
                onComplete();

            Reset();

            return false;
        }

        bool RunTasks()
        {
            while (_listOfStacks.Count > 0)
            {
#if TO_IMPLEMENT_PROPERLY   
                _subprogress = 0;

                for (int i = 0; i < _listOfStacks.Count; ++i)
                {
                    Stack<IEnumerator> stack = _listOfStacks[i];

                    if (stack.Count > 0)
                    {
                        IEnumerator ce = stack.Peek(); //without popping it.

                        if (ce is EnumeratorWithProgress)
                            _subprogress += (ce as EnumeratorWithProgress).progress;
                    }
                }

                _subprogress /= registeredEnumerators.Count;
#endif
                for (int index = _index; index < _listOfStacks.Count; ++index)
                {
                    Stack<IEnumerator> stack = _listOfStacks[index];

                    if (stack.Count > 0)
                    {
                        IEnumerator ce = stack.Peek(); //without popping it.
                        _current = ce;

                        if (ce.MoveNext() == false)
                        {
                            stack.Pop(); //now it can be popped

                            if (ce.Current == Break.AndStop)
                            {
                                _currentWrapper = ce.Current;

                                return false;
                            }
                        }
                        else //ok the iteration is not over
                        {
                            var current = ce.Current;

                            if (current == ce)
                                throw new Exception("An enumerator returning itself is not supported");

                            if (ce is TaskCollection == false && 
                                current != null && current != Break.It
                                 && current != Break.AndStop)
                            {
                                IEnumerator result = StandardEnumeratorCheck(current);
                                if (result != null)
                                {
                                    stack.Push(result);

                                    continue;
                                }
                                //in all the cases above, the task collection is not meant to yield
                            }
                            else
                            //Break.It breaks only the current task collection 
                            //enumeration but allows the parent task to continue
                            //yield break would instead stops only the single task
                            //BreakAndStop bubble until it gets to the TaskRoutine
                            //which is stopped and triggers the OnStop callback
                            if (current == Break.It || ce.Current == Break.AndStop)
                            {
                                _currentWrapper = ce.Current;

                                return false;
                            }

                            _index = index + 1;

                            return true;
                        }
                    }
                    else
                    {
                        _listOfStacks.UnorderedRemoveAt(index--);
#if TO_IMPLEMENT_PROPERLY
                        _progress = (registeredEnumerators.Count - _listOfStacks.Count) / (float)registeredEnumerators.Count;
                        _subprogress = 0.0f;
#endif
                    }
                }

                _index = 0;
            }
            return false;
        }

        public object Current
        {
            get { return _currentWrapper; }
        }

        object  _current;
        object  _currentWrapper;

        int     _index;

#if TO_IMPLEMENT_PROPERLY
        float 			_progress;
        float           _subprogress;
#endif
        internal class ParallelTask
        {
            public object current {  get {  return _parent._current; } }

            public ParallelTask(ParallelTaskCollection parent)
            {
                _parent = parent;
            }

            public void Add(IEnumerator task)
            {
                _parent.Add(task);
            }

            readonly ParallelTaskCollection _parent;
        }
    }
}

