using System;
using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks
{
    public class ParallelTaskCollection: TaskCollection
    {
        public event Action        		    onComplete;
        public event Func<Exception, bool>  onException;

        public ParallelTaskCollection()
        {
            _currentWrapper = new ParallelTask(this);
            
            _name = "ParallelTaskCollection".FastConcat(GetHashCode());
        }
        
        public ParallelTaskCollection(string name)
        {
            _currentWrapper = new ParallelTask(this);
            _name = name;
        }

        public ParallelTaskCollection(int initialSize, string name = null) : base(initialSize)
        {
            _currentWrapper = new ParallelTask(this);
            
            if (name == null)
                _name = "ParallelTaskCollection".FastConcat(GetHashCode());
            else
                _name = name;
        }

        public ParallelTaskCollection(IEnumerator[] ptasks) : this()
        {
            for (int i = 0; i < ptasks.Length; i++)
                Add(ptasks[i]);
        }

        public override String ToString()
        {
            return _name;
        }

        public override void Reset()
        {
            ResetIndices();
            
            for (int i = 0; i < base._listOfStacks.Count; i++)
                _listOfStacks[i].Peek().Reset();
        }

        public new void Clear()
        {
            base.Clear();
            
            ResetIndices();
        }

        public override bool MoveNext()
        {
            isRunning = true;

            try
            {
                if (RunTasks()) return true;
                
                isRunning = false;
                ResetIndices();
                
                if (onComplete != null)
                    onComplete();
            }
            catch (Exception e)
            {
                if (onException != null)
                {
                    var mustComplete = onException(e);

                    if (mustComplete)
                    {
                        isRunning = false;
                    }
                }
                
                throw;
            }
            
            return false;
        }

        void ResetIndices()
        {
            _offset = 0;
        }

        bool RunTasks()
        {
            var count = _listOfStacks.Count;
            while (count - _offset > 0)
            {
                for (int index = 0; index < count - _offset; ++index)
                {
                    Stack<IEnumerator> stack = _listOfStacks[index];

                    if (stack.Count > 0)
                    {
                        IEnumerator ce = stack.Peek();  //get the current task to execute
                        _current = ce;
                        
                        bool isDone = !ce.MoveNext();

                        if (isDone == true) 
                        {
                            if (ce.Current == Break.AndStop)
                            {
                                _currentWrapper = ce.Current;

                                return false;
                            }

                            if (stack.Count > 1)
                                stack.Pop(); //now it can be popped
                            else
                            {
                                //in order to be able to reuse the task collection, we will keep the stack 
                                //in its original state. The tasks will be shuffled, but due to the nature
                                //of the parallel execution, it doesn't matter.
                                index = RemoveStack(index); 
                            }
                        }
                        else //ok the iteration is not over
                        {
                            _current = ce.Current;

                            if (_current == ce)
                                throw new Exception("An enumerator returning itself is not supported");

                            if (ce is TaskCollection == false && 
                                _current != null && _current != Break.It
                                 && _current != Break.AndStop)
                            {
                                IEnumerator result = StandardEnumeratorCheck(_current);
                                if (result != null)
                                    stack.Push(result); //push the new yielded task and execute it immediately
                            }
                            else
                            //Break.It breaks only the current task collection 
                            //enumeration but allows the parent task to continue
                            //yield break would instead stops only the single task
                            //BreakAndStop bubble until it gets to the TaskRoutine
                            //which is stopped and triggers the OnStop callback
                            if (_current == Break.It || _current == Break.AndStop)
                            {
                                _currentWrapper = ce.Current;

                                return false;
                            }
                        }
                    }
                }

                return true;
            }
            return false;
        }

        int RemoveStack(int index)
        {
            var lastIndex = _listOfStacks.Count - _offset - 1;

            _offset++;

            if (index == lastIndex)
                return index;

            var item = _listOfStacks[lastIndex];
            _listOfStacks[lastIndex] = _listOfStacks[index];
            _listOfStacks[index] = item;

            return --index;
        }

        public override object Current
        {
            get { return _currentWrapper; }
        }

        object      _current;
        int         _offset;

        object  _currentWrapper;
        string _name;

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

