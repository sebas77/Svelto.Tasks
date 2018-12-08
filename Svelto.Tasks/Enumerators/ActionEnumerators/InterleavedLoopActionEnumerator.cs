using System;
using System.Collections;

namespace Svelto.Tasks.Enumerators
{
    /// <summary>
    /// yield an action every interval
    /// </summary>
    public class InterleavedLoopActionEnumerator : IEnumerator
    {
        public InterleavedLoopActionEnumerator(Action action, int intervalMS)
        {
            _action   = action;
            _then     = DateTime.UtcNow.AddMilliseconds(intervalMS);
            _interval = intervalMS;
        }

        public object Current
        {
            get { return null; }
        }

        public bool MoveNext()
        {
            if (DateTime.UtcNow > _then)
            {
                _action();

                _then = DateTime.UtcNow.AddMilliseconds(_interval);
            }
            return true;
        }

        public void Reset()
        {
            _then = DateTime.UtcNow.AddMilliseconds(_interval);
        }
        
        public override string ToString()
        {
            if (_name == null)
            {
                var method = _action.GetMethodInfoEx();

                _name = method.GetDeclaringType().Name.FastConcat(".", method.Name);
            }

            return _name;
        }
        
        string _name;
        readonly Action _action;
        DateTime        _then = DateTime.MaxValue;
        readonly int    _interval;
    }
}