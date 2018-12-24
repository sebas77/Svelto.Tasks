using System;
using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks.Enumerators
{
    public struct ReusableWaitForSecondsEnumerator:IEnumerator<TaskContract>
    {
        public ReusableWaitForSecondsEnumerator(float seconds):this()
        {
            _seconds = seconds;
            _init    = false;
        }

        public bool MoveNext()
        {
            if (_init == false)
            {
                _future = DateTime.UtcNow.AddSeconds(_seconds);
                _init   = true;
            }
            else
            if (_future <= DateTime.UtcNow)
            {
                Reset();
                return false;
            }

            return true;
        }

        public void Reset()
        {
            _init = false;
        }

        public TaskContract Current
        {
            get { return Yield.It; }
        }

        public void Reset(float seconds)
        {
            _seconds = seconds;
            _init    = false;
        }

        object IEnumerator.Current { get { return null; } }
        
        public void Dispose()
        {}

        DateTime _future;
        float    _seconds;
        bool     _init;
    }
}