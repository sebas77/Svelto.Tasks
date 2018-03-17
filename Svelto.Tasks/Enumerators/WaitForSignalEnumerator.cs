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

        public WaitForSignalEnumerator(string name, float timeout = 100, bool autoreset = true)
        {
            _initialTimeOut = timeout;
            _timeout = timeout;
            _autoreset = autoreset;
            _name = name;
            ThreadUtility.MemoryBarrier();
        }
        
        public WaitForSignalEnumerator(string name, Func<bool> extraCondition, float timeout = 100, bool autoreset = true):this(name, timeout, autoreset)
        {
            _extraCondition = extraCondition;
        }

        public bool MoveNext()
        {
            if (_timeout == _initialTimeOut)
                _then = DateTime.UtcNow;

            ThreadUtility.MemoryBarrier();

            var isDone = _signal || _timeout < 0;
            if (_extraCondition != null) isDone |= _extraCondition();
            if (_autoreset == true && isDone == true)
            {
                Reset();
                return false;
            }
            
            _timeout -= (float)(DateTime.UtcNow - _then).TotalMilliseconds;
            _then = DateTime.UtcNow;

            if (_timeout < 0)
                Utility.Console.LogWarning("WaitForSignalEnumerator ".FastConcat(_name, " timedOut"));

            ThreadUtility.TakeItEasy();

            return !isDone;
        }

        public void Reset()
        {
            _signal = false;
            _return = null;
            _timeout = _initialTimeOut;
            
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
            DBC.Check.Require(_autoreset == false, "Can't check if done if the signal auto resets, change behaviour through the constructor parameter");
            
            return _signal;
        }
        
        volatile bool   _signal;
        volatile object _return;
        volatile float _timeout;

        readonly bool       _autoreset;
        readonly Func<bool> _extraCondition;
        
        readonly float      _initialTimeOut;
        DateTime            _then;
        string              _name;
    }
}