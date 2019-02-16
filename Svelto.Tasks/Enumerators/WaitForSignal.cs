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
    public abstract class WaitForSignal<T> where T:WaitForSignal<T>
    {
        /// <summary>
        /// the signal times out automatically, so specify the time out time according your needs. Autoreset
        /// means that the enumerator is reusable right after it has been completed without calling Reset()
        /// explicitly
        /// </summary>
        /// <param name="name"></param>
        /// <param name="timeout"></param>
        /// <param name="autoreset"></param>
        public WaitForSignal(string name, float timeout = 1000, bool autoreset = true, bool startUnlocked = false)
        {
            _waitBack = new WaitBackC(timeout, !startUnlocked);
            _wait = new WaitBackC(timeout, startUnlocked);
        }

        public void Signal()
        {
            _wait.Signal();
        }
        
        public void SignalBack()
        {
            _waitBack.Signal();
        }
        
        public IEnumerator Wait()
        {
            return _wait;
        }

        public IEnumerator WaitBack()
        {
            return _waitBack;
        }

        class WaitBackC : IEnumerator
        {
            internal WaitBackC(float timeout , bool startUnlocked)
            {
                _initialTimeOut = timeout;
                _autoreset      = true;
                _name           = "waitBack";
                _signal         = startUnlocked;
            }
            
            public bool MoveNext()
            {
                if (_started == false)
                {
                    _started = true;
                    _then    = DateTime.Now.AddMilliseconds(_initialTimeOut);
                }

                var timedOut = DateTime.Now > _then;
                _isDone = ThreadUtility.VolatileRead(ref _signal) || timedOut;
            
                if (_isDone == true)
                {
                    if (_autoreset == true)
                        Reset();
                
                    if (timedOut)
                        Console.LogWarning("WaitForSignalEnumerator ".FastConcat(_name, " timedOut"));
                
                    return false;
                }
            
                return !_isDone;
            }
            
            internal void Signal()
            {
                ThreadUtility.VolatileWrite(ref _signal, true);
            }
            
            public bool isDone()
            {
                DBC.Tasks.Check.Require(_autoreset == false, "Can't check if done if the signal auto resets, " +
                                                             "change behaviour through the constructor parameter");
            
                return _isDone;
            }

            public void Reset()
            {
                _signal  = false;
                _started = false;
            
                ThreadUtility.MemoryBarrier();
            }

            public object Current { get; }
            
            readonly float  _initialTimeOut;
            readonly bool   _autoreset;
            readonly string _name;
        
            bool     _signal;
            bool     _started;
            DateTime _then;
            bool     _isDone;
        }
        
        readonly WaitBackC  _waitBack;
        readonly WaitBackC  _wait;
    }
}