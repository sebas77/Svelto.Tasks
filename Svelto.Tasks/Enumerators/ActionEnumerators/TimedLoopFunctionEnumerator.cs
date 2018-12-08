using System;
using System.Collections;

namespace Svelto.Tasks.Enumerators
{
    /// <summary>
    /// Yield a function that takes as parameter the time passed since the last yield
    /// </summary>
    public class TimedLoopFunctionEnumerator:IEnumerator
    {
        public TimedLoopFunctionEnumerator(Func<float, bool> action)
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
            bool ret = _action(lapse);
            _then = DateTime.UtcNow;
            return ret;
        }

        public void Reset()
        {}

        public override string ToString()
        {
            if (_name == null)
            {
                var method = _action.GetMethodInfoEx();

                _name = method.GetDeclaringType().Name.FastConcat(".", method.Name);
            }

            return _name;
        }
        
        string            _name;
        DateTime          _then = DateTime.MaxValue;
        
        readonly Func<float, bool> _action;
    }
}