using System.Collections;
using Svelto.Utilities;

namespace Svelto.Tasks
{
    /// <summary>
    /// The Continuation Wrapper contains a valid value until the task is not stopped. After that it should be released. 
    /// </summary>
    public class ContinuationEnumerator : IEnumerator
    {
        public bool MoveNext()
        {
            if (ThreadUtility.VolatileRead(ref _completed) == true)
            {
                Reset();
                return false;
            }

            return true;
        }

        internal void Completed()
        {
            ThreadUtility.VolatileWrite(ref _completed, true);
        }

        public bool completed
        {
            get { return _completed; }
        }

        public void Reset()
        {
            _completed = false;
        }

        public object Current
        {
            get { return null; }
        }
       
        bool _completed;
    }
}