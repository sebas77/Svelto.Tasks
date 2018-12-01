using System.Collections;

namespace Svelto.Tasks.Enumerators
{
    public class WaitForSecondsEnumerator:IEnumerator
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

        public void Reset(float seconds)
        {
            _wait.Reset(seconds);
        }

        public object Current { get { return null; } }

        ReusableWaitForSecondsEnumerator _wait;
    }
}

