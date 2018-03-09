using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Threading;
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

        public WaitForSignalEnumerator(double timeout = 10, bool autoreset = true)
        {
            _timeout = timeout;
            _autoreset = autoreset;
        }
        
        public WaitForSignalEnumerator(Func<bool> extraCondition, double timeout = 10, bool autoreset = true):this(timeout, autoreset)
        {
            _extraCondition = extraCondition;
        }

        public bool MoveNext()
        {
            var then = DateTime.UtcNow;
            
            ThreadUtility.MemoryBarrier();

            var isDone = _signal || _timeout < 0;
            if (_extraCondition != null) isDone |= _extraCondition();
            if (_autoreset == true && isDone == true)
            {
                Reset();
                return false;
            }
            
            _timeout -= (DateTime.UtcNow - then).TotalMilliseconds;
            
            if (_timeout < 0)
                Utility.Console.LogWarning("WaitForSignalEnumerator ".FastConcat(ToString(), " timedOut"));
            
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

        public void WaitForSignal()
        {
            while (MoveNext()) ThreadUtility.Yield();
        }

        public bool isDone()
        {
            DBC.Check.Require(_autoreset == false, "Can't check if done if the signal auto resets, change behaviour through the constructor parameter");
            
            return _signal;
        }
        
        volatile bool _signal;
        volatile object _return;

        readonly bool _autoreset;
        readonly Func<bool> _extraCondition;
        double _timeout;
    }
}