using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.Enumerators
{
    public class ContinuationEnumerator : IEnumerator<TaskContract>
    {
        public TaskContract Current => TaskContract.Yield.It;
        object IEnumerator.Current => throw new NotSupportedException();

        public bool MoveNext()
        {
            _continuation();
            return false;
        }

        public void Reset() { }

        public void Dispose()
        {
            _continuation = null;
            ContinuationEnumeratorPool.PushBack(this);
        }

        internal void SetContinuation(Action continuation)
        {
            _continuation = continuation;
        }

        Action _continuation;
    }
}