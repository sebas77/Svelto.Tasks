using System;
using System.Collections;

namespace Svelto.Tasks
{
    public class WaitForSecondsEnumerator:IEnumerator
    {
        public WaitForSecondsEnumerator(float seconds)
        {
            _seconds = seconds;
            _future = DateTime.UtcNow.AddSeconds(seconds);
        }

        public bool MoveNext()
        {
            return _future >= DateTime.UtcNow;
        }

        public void Reset()
        {
            _future = DateTime.UtcNow.AddSeconds(_seconds);
        }

        public object Current { get { return null; } }

        DateTime _future;
        float _seconds;
    }
}

