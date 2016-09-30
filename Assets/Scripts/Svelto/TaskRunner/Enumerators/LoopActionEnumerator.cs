using System;
using System.Collections;

namespace Svelto.Tasks
{
    public class LoopActionEnumerator<T> : IEnumerator
    {
        public LoopActionEnumerator(Action<T> action)
        {
            _action = action;
        }

        public LoopActionEnumerator(Action<T> action, T parameter)
        {
            _action = action;
            _parameter = parameter;
        }

        Action<T> _action;
        T _parameter;

        public object Current
        {
            get { return null; }
        }

        public bool MoveNext()
        {
            _action(_parameter);
            return true;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }

    public class LoopActionEnumerator:IEnumerator
    {
        public LoopActionEnumerator(Action action)
        {
            _action = action;
        }

        Action _action;

        public object Current
        {
            get { return null; }
        }

        public bool MoveNext()
        {
            _action();
            return true;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
}
