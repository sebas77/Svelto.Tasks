#if UNITY_STANDALONE || UNITY_WEBPLAYER || UNITY_IPHONE || UNITY_ANDROID || UNITY_EDITOR
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
                            var current = ce.Current;
                            if (current != ce && current != null) 
                            {
                               var enumerator = current as IEnumerator;
                               if (enumerator != null)
                               {
                                   CheckForToken(current);
                                   stack.Push(enumerator);
                                   continue;
                               }

                               var task = current as IAbstractTask;
                               if (task != null)
                               {
                                   stack.Push(CreateTaskWrapper(task));
                                   continue;
                               }

                               var ptasks = current as IEnumerator[];
                               if (ptasks != null)
                               {
                                   stack.Push(new ParallelTaskCollection(ptasks));

                                   continue;
                               }
                            }

                            if (current == Break.It || current == null)
                                _current = current;
                            else
                            {
                                _markUP.Current = current;
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
            
            isRunning = false;
            
            if (onComplete != null)
                onComplete();

            Reset();

            return false;
        }

        public object Current
        {
            get { return _current; }
        }

        ParallelYield _markUP = new ParallelYield(); 

        object          _current;
        int             _index;
        

#if TO_IMPLEMENT_PROPERLY
        float 					 _progress;
        float                    _subprogress;
#endif
    }

    public class ParallelTaskCollection<Token>: ParallelTaskCollection
    {
        override protected TaskWrapper CreateTaskWrapper(IAbstractTask task)
        {
            return new TaskWrapper<Token> (task) { token = _token };
        }

        public ParallelTaskCollection(Token token) { _token = token; }

        override protected void CheckForToken (object current)
        {
            var task = current as IChainLink<Token>;
            if (task != null)
                task.token = _token;
        }

        Token     _token;
    }
}
#endif
