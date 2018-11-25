#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Enumerators
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
#endif