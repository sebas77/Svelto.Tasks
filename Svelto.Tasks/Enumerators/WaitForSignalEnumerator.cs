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

        public bool MoveNext()
        {
            ThreadUtility.Yield();
            ThreadUtility.MemoryBarrier();

            if (_autoreset == true && _signal == true)
            {
                Reset();
                return false;
            }
            
            return !_signal;
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
    }
}