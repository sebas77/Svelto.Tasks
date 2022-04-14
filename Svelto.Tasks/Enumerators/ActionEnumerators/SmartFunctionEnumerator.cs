using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Utilities;

namespace Svelto.Tasks.Enumerators
{
    /// <summary>
    /// Yield a function that control the flow execution through the return value.
    /// </summary>
    /// <typeparam name="TVal">
    /// facilitate the use of counters that can be passed by reference to the callback function
    /// </typeparam>
    public class SmartFunctionEnumerator<TVal>: IEnumerator<TaskContract>
    {
        public SmartFunctionEnumerator(FuncRef<TVal, bool> func, TVal value)
        {
            _func  = func;
            _value = value;
        }

        public bool MoveNext() { return _func(ref _value); }
        public void Reset() {}

        public TaskContract Current => Yield.It;
        object IEnumerator.Current => throw new NotSupportedException();
        public TVal value => _value;
        
        public override string ToString()
        {
            if (_name == null)
            {
                var method = _func.GetMethodInfoEx();

                _name = method.GetDeclaringType().Name.FastConcat(".", method.Name);
            }

            return _name;
        }

        public void Dispose() {}

        readonly FuncRef<TVal, bool> _func;
        TVal                         _value;
        string                       _name;
    }
}