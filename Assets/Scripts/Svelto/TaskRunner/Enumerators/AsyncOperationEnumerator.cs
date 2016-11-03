using UnityEngine;

namespace Svelto.Tasks
{
    public class AsyncOperationEnumerator : ParallelEnumerator
    {
        AsyncOperation _asyncOp;

        public AsyncOperationEnumerator(AsyncOperation www)
        {
            _asyncOp = www;
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
