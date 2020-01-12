using System.Collections;
using System.Collections.Generic;
using Svelto.Utilities;

namespace Svelto.Tasks.Enumerators
{
    /// <summary>
    /// /// Yield a function that control the flow execution through the return value.
    /// </summary>
    /// <typeparam name="T">
    /// facilitate the use of counters that can be passed by reference to the callback function
    /// </typeparam>
    public class SmartFunctionEnumerator<T>:IEnumerator, IEnumerator<T>
    {
        public SmartFunctionEnumerator(FuncRef<T, bool> func)
        {
            _func  = func;
            _value = default(T);
        }

        public T field
        {
            get { return _value; }
        }

        public bool MoveNext()
        {
            return _func(ref _value);
        }

        public void Reset()
        {}

        T IEnumerator<T>.Current
        {
            get { return _value; }
        }

        public object Current
        {
            get { return null; }
        }
        
        public override string ToString()
        {
            if (_name == null)
            {
                var method = _func.GetMethodInfoEx();

                _name = method.GetDeclaringType().Name.FastConcat(".", method.Name);
            }

            return _name;
        }

        public void Dispose()
        {}

        FuncRef<T, bool> _func;
        T                _value;

        string _name;
        T _current;
    }
}