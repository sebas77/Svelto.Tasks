#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Svelto.Tasks.Enumerators
{
    public class AsyncOperationEnumerator: IEnumerator<TaskContract>
    {
        readonly AsyncOperation _asyncOp;
        public AsyncOperationEnumerator(AsyncOperation async)
        {
            _asyncOp = async;
        }

        object IEnumerator.Current { get { return _asyncOp; } }

        public bool MoveNext()
        {
            return _asyncOp.isDone == false;
        }

        public void Reset()
        {}

        public TaskContract Current
        {
            get { return Yield.It; }
        }

        public void Dispose()
        {}
    }
}
#endif