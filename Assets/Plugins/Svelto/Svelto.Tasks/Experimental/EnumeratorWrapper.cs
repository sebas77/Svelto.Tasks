using System;
using System.Collections;

namespace Svelto.Tasks.Experimental
{
    public class EnumeratorWrapper<T, U>
    {
        public class Token
        {
            public U value;
            public T current;
        }

        public EnumeratorWrapper(Func<Token, IEnumerator> enumerator)
        {
            _token = new Token();
            _enumerator = enumerator(_token);
        }

        public IEnumerator Return(U value)
        {
            _token.value = value;
            return _enumerator;
        }

        public T Current { get { return _token.current; } }

        Token       _token;
        IEnumerator _enumerator;
    }
}
