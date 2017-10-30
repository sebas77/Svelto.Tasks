using System;
using System.Collections;

namespace Svelto.Tasks
{
    public class SafeEnumerator : IEnumerator
    {
        public bool succeeded { get; private set; }
        public Exception error { get; private set; }

        public SafeEnumerator(IEnumerator enumerator)
        {
            _enumerator = enumerator;
        }

        public object Current { get { return _enumerator.Current; } }

        public bool MoveNext()
        {
            bool moveNext = false;
            try
            {
                moveNext = _enumerator.MoveNext();
                if(moveNext == false)
                    succeeded = true;
            }
            catch(Exception e)
            {
                succeeded = false;
                error = e;
            }
            return moveNext;
        }

        public void Reset()
        {
            _enumerator.Reset();
        }

        IEnumerator _enumerator;
    }
}
