using System;
using System.Collections;

namespace Svelto.Tasks
{
    public class SerialTaskCollection: TaskCollection, IEnumerator
    {
        public event Action		onComplete;

        public SerialTaskCollection(int size):base(size)
        {}

        public SerialTaskCollection()        
        {}
#if TO_IMPLEMENT_PROPERLY
        override public float 	progress { get { return _progress + _subProgress;} }
#endif
#if TO_IMPLEMENT_PROPERLY     
        public SerialTaskCollection()        
        {
            _progress = 0.0f;
            _subProgress = 0.0f;
        }
#endif
        new public void Reset()
        {
            base.Reset();
            _index = 0;
        }

        public bool MoveNext()
        {
            isRunning = true;
#if TO_IMPLEMENT_PROPERLY            
            int startingCount = registeredEnumerators.Count;
#endif
            while (_index < _listOfStacks.Count)
            {
                var stack = _listOfStacks[_index];

                while (stack.Count > 0)
                {
                    var ce = stack.Peek(); //get the current task to execute

                    if (ce.MoveNext() == false)
                    {
#if TO_IMPLEMENT_PROPERLY
                        _progress = (float)(startingCount - registeredEnumerators.Count) / (float)startingCount;
                        _subProgress = 0;
#endif
                        stack.Pop(); //task is done (the iteration is over)
                    }
                    else
                    {
                        _current = ce.Current;
                        if (_current != ce && _current != null)
                        {
                            var enumerator = _current as IEnumerator;
                            if (enumerator != null)
                            {
                                CheckForToken(_current);
                                stack.Push(enumerator); //it's pushed because it can yield another IEnumerator on its own
#if TO_IMPLEMENT_PROPERLY
                                push(subprogress);
#endif
                                continue;
                            }

                            var task = _current as IAbstractTask;
                            if (task != null)
                            {
                                stack.Push(CreateTaskWrapper(task));

                                continue;
                            }

                            var ptasks = _current as IEnumerator[];
                            if (ptasks != null)
                            {
                                stack.Push(new ParallelTaskCollection(ptasks));

                                continue;
                            }
                        }

                        return true;
#if TO_IMPLEMENT_PROPERLY
                        if (ce is AsyncTask) //asyn
                            _subProgress = (ce as AsyncTask).task.progress * (((float)(startingCount - (registeredEnumerators.Count - 1)) / (float)startingCount) - progress);
                        else
                        if (ce is EnumeratorWithProgress) //asyn
                            _subProgress = (ce as EnumeratorWithProgress).progress / (float)registeredEnumerators.Count;
#endif
                    }
                }

                _index++;
            }

            if (onComplete != null)
                onComplete();

            isRunning = false;
            Reset();

            return false;
        }

        int _index;

#if TO_IMPLEMENT_PROPERLY
        float 	_progress;
        float 	_subProgress;
#endif
        public object Current
        {
            get { return _current; }
        }

        object _current;
    }
}

