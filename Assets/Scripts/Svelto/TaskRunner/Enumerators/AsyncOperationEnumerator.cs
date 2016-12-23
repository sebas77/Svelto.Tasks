using UnityEngine;

namespace Svelto.Tasks
{
    public class AsyncOperationEnumerator : IParallelEnumerator
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
