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

        public override string ToString()
        {
            var method = _action.Method;

            return method.ReflectedType.Name + "." + method.Name;
        }

        Action<T> _action;
        T _parameter;
    }

    public class LoopActionEnumerator:IEnumerator
    {
        public LoopActionEnumerator(Action action)
        {
            _action = action;
        }

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

        public override string ToString()
        {
            var method = _action.Method;

            return method.ReflectedType.Name + "." + method.Name;
        }

        Action _action;
    }

    public class TimedLoopActionEnumerator:IEnumerator
    {
        public TimedLoopActionEnumerator(Action<float> action)
        {
            _action = action;
        }

        public object Current
        {
            get { return null; }
        }

        public bool MoveNext()
        {
            float lapse = Math.Max(0, (float)(DateTime.UtcNow - _then).TotalSeconds);
            _action(lapse);
            _then = DateTime.UtcNow;
            return true;
        }

        public override string ToString()
        {
            var method = _action.Method;

            return method.ReflectedType.Name + "." + method.Name;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        Action<float>   _action;
        DateTime        _then = DateTime.MaxValue;
    }
}
