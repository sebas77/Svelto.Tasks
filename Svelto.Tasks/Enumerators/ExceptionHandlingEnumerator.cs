using System;
using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks.Enumerators
{
    public class ExceptionHandlingEnumerator : IEnumerator<TaskContract>
    {
        public bool succeeded { get; private set; }
        public Exception error { get; private set; }

        public ExceptionHandlingEnumerator(IEnumerator enumerator)
        {
            _enumerator = enumerator;
        }

        object IEnumerator.Current { get { return _enumerator.Current; } }

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

        public TaskContract Current
        {
            get { return Yield.It; }
        }

        public void Dispose()
        {
        }

        readonly IEnumerator _enumerator;
    }
}
