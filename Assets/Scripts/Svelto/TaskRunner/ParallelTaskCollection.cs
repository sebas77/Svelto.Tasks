using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    public class ParallelTaskCollection: TaskCollection, IEnumerator
    {
        public event Action		onComplete;
#if TO_IMPLEMENT_PROPERLY
        override public float progress { get { return _progress + _subprogress; } }
#endif 
        public ParallelTaskCollection()
        {}

        public ParallelTaskCollection(int initialSize):base(initialSize)
        { }

        public ParallelTaskCollection(IEnumerator[] ptasks)
        {
            for (int i = 0; i < ptasks.Length; i++)
                Add(ptasks[i]);
        }

        new public void Reset()
        {
            base.Reset();
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
                                throw new Exception("An enumerator returning itself is not supported");

                            if (_current != null && _current != Break.It)
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
                            else
                            //an Enumerator that must be handled by Unity
                            //(www, asyncOp) inherits from ParalleEnumerator
                            //to instruct the runners that it doesn't have
                            //to wait for unity to finish does instructions.
                            //It makes sense only for runners that support
                            //unity operations, otherwise it will be ignored
                            if (ce is IParallelEnumerator)
                            {
                                _markUP.Current = _current;
                                _current = _markUP;
                            }

                            _index = index + 1;

                            return true;
                        }
                    }
                    else
                    {
                        _listOfStacks.UnorderredRemoveAt(index--);
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
            get { return _current; }
        }

        ParallelYield   _markUP = new ParallelYield(); 

        object          _current;
        int             _index; 
#if TO_IMPLEMENT_PROPERLY
        float 			_progress;
        float           _subprogress;
#endif
    }
}

