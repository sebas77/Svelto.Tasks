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

        public WaitForSignalEnumerator(string name, float timeout = 1000, bool autoreset = true)
        {
            _initialTimeOut = timeout;
            _autoreset = autoreset;
            _name = name;
            
            ThreadUtility.MemoryBarrier();
        }        
        public WaitForSignalEnumerator(string name, Func<bool> extraDoneCondition, float timeout = 1000, bool autoreset = true):this(name, timeout, autoreset)
        {
            _extraDoneCondition = extraDoneCondition;
        }

        public bool MoveNext()
        {
            if (_started == false)
            {
                _started = true;
                _then = DateTime.Now.AddMilliseconds(_initialTimeOut);
            }
            ThreadUtility.MemoryBarrier();

            var timedOut = DateTime.Now > _then;
            var isDone = _signal || timedOut;
            
            if (_extraDoneCondition != null) isDone |= _extraDoneCondition();
            
            if (isDone == true)
            {
                if (_autoreset == true)
                    Reset();
                
                if (timedOut)
                    Svelto.Utilities.Console.LogWarning("WaitForSignalEnumerator ".FastConcat(_name, " timedOut"));
                
                return false;
            }
            
            ThreadUtility.Yield();

            return !isDone;
        }

        public void Reset()
        {
            _signal = false;
            _then = DateTime.Now.AddMilliseconds(_initialTimeOut);
            
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
            DBC.Tasks.Check.Require(_autoreset == false, "Can't check if done if the signal auto resets, change behaviour through the constructor parameter");
            
            return _signal;
        }
        
        volatile bool   _signal;
        volatile object _return;
        
        readonly bool       _autoreset;
        readonly Func<bool> _extraDoneCondition;
        
        readonly float      _initialTimeOut;
        DateTime            _then;
        string              _name;
        bool                _started;
    }
}