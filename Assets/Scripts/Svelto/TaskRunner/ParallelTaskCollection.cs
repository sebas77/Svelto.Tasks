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
            _parallelTask = new ParallelTask(_current, this);
        }

        public ParallelTaskCollection(int initialSize) : base(initialSize)
        {
            _parallelTask = new ParallelTask(_current, this);
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

                        if (ce.MoveNext() == false)
                            stack.Pop(); //now it can be popped
                        else //ok the iteration is not over
                        {
                            _current = ce.Current;

                            if (_current == ce)
                                throw new Exception
                            ("An enumerator returning itself is not supported");

                            if ((ce is TaskCollection == false) && 
                                _current != null && _current != Break.It)
                            {
                                IEnumerator result = StandardEnumeratorCheck(_current);
                                if (result != null)
                                {
                                    stack.Push(result);

                                    continue;
                                }
                                //in all the cases above, the task collection is not meant to yield
                            }
                            else 
                            if (_current == Break.It)
                                return false;

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
            get { return _parallelTask; }
        }

        object          _current;
        int             _index;

        readonly ParallelTask    _parallelTask;
#if TO_IMPLEMENT_PROPERLY
        float 			_progress;
        float           _subprogress;
#endif
        internal class ParallelTask
        {
            public object current {  get {  return _current;} }

            public ParallelTask(object current, ParallelTaskCollection parent)
            {
                _current = current;
                _parent = parent;
            }

            public void Add(IEnumerator task)
            {
                _parent.Add(task);
            }

            readonly object _current;
            readonly ParallelTaskCollection _parent;
        }
    }
}

