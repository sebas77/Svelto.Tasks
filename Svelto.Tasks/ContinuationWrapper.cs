using System.Collections;
using Svelto.Utilities;

namespace Svelto.Tasks
{
    /// <summary>
    /// The Continuation Wrapper contains a valid value until the task is not stopped. After that it should be released. 
    /// </summary>
    public class ContinuationWrapper : IEnumerator
    {
        public bool MoveNext()
        {
            ThreadUtility.MemoryBarrier();
            if (_completed == true)
            {
                Reset();
                return false;
            }

            return true;
        }

        internal void Completed()
        {
            _completed = true;
            ThreadUtility.MemoryBarrier();
        }

        public bool completed
        {
            get { return _completed; }
        }

        public void Reset()
        {
            _completed = false;
            ThreadUtility.MemoryBarrier();
        }

        public object Current
        {
            get { return null; }
        }

        volatile bool _completed;
    }
}