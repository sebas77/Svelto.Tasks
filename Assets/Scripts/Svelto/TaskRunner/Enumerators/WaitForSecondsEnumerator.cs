using System;
using System.Collections;

namespace Svelto.Tasks.Enumerators
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
            if (_future <= DateTime.UtcNow)
            {
                Reset();
                return false;
            }

            return true;
        }

        public void Reset()
        {
            _future = DateTime.UtcNow.AddSeconds(_seconds);
        }

        public void Reset(float seconds)
        {
            _seconds = seconds;
            _future = DateTime.UtcNow.AddSeconds(_seconds);
        }

        public object Current { get { return null; } }

        DateTime _future;
        float _seconds;
    }
}

