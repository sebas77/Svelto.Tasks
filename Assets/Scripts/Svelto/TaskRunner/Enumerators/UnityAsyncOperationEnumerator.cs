using System.Collections;
using UnityEngine;

namespace Svelto.Tasks
{
    class UnityAsyncOperationEnumerator: IEnumerator
    {
        AsyncOperation _asyncOp;

        public UnityAsyncOperationEnumerator(AsyncOperation async)
        {
            _asyncOp = async;
        }

        public object Current { get { return _asyncOp; } }

        public bool MoveNext()
        {
            return _asyncOp.isDone == false;
        }

        public void Reset()
        {}
    }
}
