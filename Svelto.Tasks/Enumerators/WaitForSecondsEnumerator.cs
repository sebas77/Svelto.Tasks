using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks.Enumerators
{
    public class WaitForSecondsEnumerator:IEnumerator<TaskContract>
    {
        public WaitForSecondsEnumerator(float seconds)
        {
            _wait = new ReusableWaitForSecondsEnumerator(seconds); 
        }

        public bool MoveNext()
        {
            return _wait.MoveNext();
        }

        public void Reset()
        {
            _wait.Reset();
        }

        public TaskContract Current
        {
            get { return Yield.It; }
        }

        public void Reset(float seconds)
        {
            _wait.Reset(seconds);
        }

        object IEnumerator.Current { get { return null; } }

        ReusableWaitForSecondsEnumerator _wait;

        public void Dispose()
        {}
    }
}

