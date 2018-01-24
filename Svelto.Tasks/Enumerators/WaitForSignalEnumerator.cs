using System;
using System.Collections;
using Svelto.Utilities;

namespace Svelto.Tasks.Enumerators
{
    public class WaitForSignalEnumerator:IEnumerator
    {
        public object Current
        {
            get
            {
                return _return;
            }
        }

        public WaitForSignalEnumerator(bool autoreset = true)
        {
            _autoreset = autoreset;
        }
        
        public WaitForSignalEnumerator(Func<bool> extraCondition, bool autoreset = true)
        {
            _autoreset = autoreset;
            _extraCondition = extraCondition;
        }

        public bool MoveNext()
        {
            ThreadUtility.MemoryBarrier();

            var isDone = _signal;
            if (_extraCondition != null) isDone |= _extraCondition();
            if (_autoreset == true && isDone == true)
            {
                Reset();
                return false;
            }
            
            return !isDone;
        }

        public void Reset()
        {
            _signal = false;
            _return = null;
            ThreadUtility.MemoryBarrier();
        }

        public void Signal()
        {
            _signal = true;
            ThreadUtility.MemoryBarrier();
        }

        public void Signal(object obj)
        {
            _signal = true;
            _return = obj;
            ThreadUtility.MemoryBarrier();
        }

        public bool isDone()
        {
            DesignByContract.Check.Require(_autoreset == false, "Can't check if done if the signal auto resets, change behaviour through the constructor parameter");
            
            return _signal;
        }
        
        volatile bool _signal;
        volatile object _return;
        
        bool _autoreset;
        Func<bool> _extraCondition;
    }
}