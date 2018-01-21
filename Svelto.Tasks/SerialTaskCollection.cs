using System;
using System.Collections;

namespace Svelto.Tasks
{
    public class SerialTaskCollection: TaskCollection
    {
        public event Action                 onComplete;
        public event Func<Exception, bool>  onException;

        public SerialTaskCollection(int size, string name = null) : base(size)
        {
            if (name == null)
                _name = "SerialTaskCollection".FastConcat(GetHashCode());
            else
                _name = name;
        }

        public SerialTaskCollection()
        {
            _name = "SerialTaskCollection".FastConcat(GetHashCode());
        }
        
        public SerialTaskCollection(string name)
        {
            _name = name;
        }

        public override String ToString()
        {
            return _name;
        }

        public override object Current
        {
            get { return _current; }
        }

        public override void Reset()
        {
            for (int i = 0; i < base._listOfStacks.Count; i++)
                _listOfStacks[i].Peek().Reset();
            
            _index = 0;
        }

        public new void Clear()
        {
            base.Clear();
            
            _index = 0;
        }

        public override bool MoveNext()
        {
            isRunning = true;

            try
            {
                if (RunTasks())
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
                    {
                        isRunning = false;

                        _index = 0;
                    }
                }

                throw;
            }
            
            isRunning = false;

            _index = 0;

            return false;
        }

        bool RunTasks()
        {
            var count = _listOfStacks.Count;
            while (_index < count)
            {
                var stack = _listOfStacks[_index];

                while (stack.Count > 0)
                {
                    var ce = stack.Peek(); //get the current task to execute
                    _current = ce;

                    if (ce.MoveNext() == false) //execute step and check if continue
                    {
                        if (ce.Current == Break.AndStop)
                        {
                            _current = ce.Current;

                            return false;
                        }

                        if (stack.Count > 1)
                            stack.Pop(); //task is done (the iteration is over)
                        else
                        {
                            //in order to be able to reuse the task collection, we will keep the stack 
                            //in its original state and move to the next task
                            _index++;
                            break;
                        }
                    }
                    else //ok the iteration is not over
                    {
                        _current = ce.Current;

                        if (_current == ce)
                            throw new Exception("An enumerator returning itself is not supported");

                        if ((ce is TaskCollection == false) 
                            && _current != null && _current != Break.It
                            && _current != Break.AndStop)
                        {
                           IEnumerator result = StandardEnumeratorCheck(_current);
                           if (result != null)
                           {
                               stack.Push(result); //push the new yielded task and execute it immediately

                                continue;
                           }
                        }
                        else
                        //Break.It breaks only the current task collection 
                        //enumeration but allows the parent task to continue
                        //yield break would instead stops only the single task
                        if (_current == Break.It || _current == Break.AndStop)
                        {
                            _current = ce.Current;

                            return false;
                        }

                        return true;
                    }
                }
            }
            return false;
        }

        internal void FastClear()
        {
            _listOfStacks.FastClear();
            _index = 0;
        }

        int _index;
        object _current;
        string _name;
    }
}

