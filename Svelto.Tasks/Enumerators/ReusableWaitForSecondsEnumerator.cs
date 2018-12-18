using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Unity;

namespace Svelto.Tasks.Enumerators
{
    public struct ReusableWaitForSecondsEnumerator:IEnumerator<TaskContract?>
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

        TaskContract? IEnumerator<TaskContract?>.Current => null;

        public void Reset(float seconds)
        {
            _seconds = seconds;
            _init    = false;
        }

        public object Current { get { return null; } }
        
        public void Dispose()
        {}

        DateTime _future;
        float    _seconds;
        bool     _init;
    }
}