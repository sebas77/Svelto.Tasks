using System;
using System.Collections;

namespace Svelto.Tasks.Enumerators
{
    /// <summary>
    /// Yield a function that control the flow execution through the return value
    /// </summary>
    public class SmartFunctionEnumerator:IEnumerator
    {
        public SmartFunctionEnumerator(Func<bool> callback)
        {
            _callback = callback;
        }

        public bool MoveNext()
        {
            return _callback.Invoke();
        }

        public void Reset()
        {}

        public object Current { get; }

        public override string ToString()
        {
            if (_name == null)
            {
                var method = _callback.GetMethodInfoEx();

                _name = method.GetDeclaringType().Name.FastConcat(".", method.Name);
            }

            return _name;
        }
        
        string _name;
        readonly Func<bool> _callback;
    }
}