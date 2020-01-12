#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Enumerators
{
    class AsyncOperationEnumerator: IEnumerator
    {
        AsyncOperation _asyncOp;

        public AsyncOperationEnumerator(AsyncOperation async)
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