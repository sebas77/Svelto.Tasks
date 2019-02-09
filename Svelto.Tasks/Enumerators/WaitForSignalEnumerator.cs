using System;
using System.Collections;
using Svelto.Utilities;

namespace Svelto.Tasks.Enumerators
{
    /// <summary>
    /// Enumerator useful to synchronize Svelto.Tasks running on different threads. It's abstract and with weird
    /// generic parameter, because I want to force the user to use specialized classes with meaningful names
    /// as way to improve the readability of the code and make its debugging simpler.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class WaitForSignalEnumerator<T>:IEnumerator where T:WaitForSignalEnumerator<T>
    {
        /// <summary>
        /// the signal times out automatically, so specify the time out time according your needs. Autoreset
        /// means that the enumerator is reusable right after it has been completed without calling Reset()
        /// explicitly
        /// </summary>
        /// <param name="name"></param>
        /// <param name="timeout"></param>
        /// <param name="autoreset"></param>
        public WaitForSignalEnumerator(string name, float timeout = 1000, bool autoreset = true, bool startUnlocked = false)
        {
            _waitBack = new WaitBackC(timeout, !startUnlocked);
            _initialTimeOut = timeout;
            _autoreset = autoreset;
            _name = name;
            _signal = startUnlocked;
        }

        public WaitForSignalEnumerator(string name, Func<bool> extraDoneCondition, float timeout = 1000, bool autoreset = true, bool startUnlocked = false):this(name, timeout, autoreset, startUnlocked)
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

            var timedOut = DateTime.Now > _then;
            _isDone = ThreadUtility.VolatileRead(ref _signal) || timedOut;
            
            if (_extraDoneCondition != null) _isDone |= _extraDoneCondition();
            
            if (_isDone == true)
            {
                if (_autoreset == true)
                    Reset();
                
                if (timedOut)
                    Svelto.Console.LogWarning("WaitForSignalEnumerator ".FastConcat(_name, " timedOut"));
                
                return false;
            }
            
            return !_isDone;
        }

        public void Reset()
        {
            _signal = false;
            _started = false;
            
            ThreadUtility.MemoryBarrier();
        }

        public void Signal()
        {
            ThreadUtility.VolatileWrite(ref _signal, true);
        }

        public void Signal(object obj)
        {
            _return = obj;
            ThreadUtility.VolatileWrite(ref _signal, true);
        }

        public bool isDone()
        {
            DBC.Tasks.Check.Require(_autoreset == false, "Can't check if done if the signal auto resets, change behaviour through the constructor parameter");
            
            return _isDone;
        }
        
        public IEnumerator WaitBack()
        {
            return _waitBack;
        }

        public void SignalBack()
        {
            _waitBack.Signal();
        }

        WaitForSignalEnumerator(float timeout , bool startUnlocked)
        {
            _initialTimeOut = timeout;
            _autoreset      = true;
            _name           = "waitBack";
            _signal         = startUnlocked;
        }

        class WaitBackC : WaitForSignalEnumerator<WaitBackC>
        {
            internal WaitBackC(float timeOut, bool startUnlocked) : base(timeOut, startUnlocked)
            {}
        }
        
        public object Current
        {
            get
            {
                return _return;
            }
        }
        
        volatile object _return;

        readonly bool       _autoreset;
        readonly Func<bool> _extraDoneCondition;
        readonly float      _initialTimeOut;
        readonly WaitBackC  _waitBack;
        readonly string     _name;
        
        bool     _signal;
        bool     _started;
        DateTime _then;
        bool     _isDone;
    }
}