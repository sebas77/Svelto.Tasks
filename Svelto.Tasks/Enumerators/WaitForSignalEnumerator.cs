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

        public bool MoveNext()
        {
            ThreadUtility.Yield();
            ThreadUtility.MemoryBarrier();

            if (_signal == true)
            {
                _signal = false;
                return false;
            }
            
            return true;
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

        volatile bool _signal;
        volatile object _return;
    }
}