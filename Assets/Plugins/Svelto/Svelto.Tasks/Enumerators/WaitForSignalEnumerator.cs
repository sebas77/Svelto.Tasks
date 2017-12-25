using System.Collections;
using Svelto.Utilities;

namespace Svelto.Tasks.Enumerators
{
    internal class WaitForSignal:IEnumerator
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
            ThreadUtility.MemoryBarrier();
            return !_signal;
        }

        public void Reset()
        {
            _signal = false;
            _return = null;
            ThreadUtility.MemoryBarrier();
        }

        internal void Signal()
        {
            _signal = true;
            ThreadUtility.MemoryBarrier();
        }

        internal void Signal(object obj)
        {
            _signal = true;
            _return = obj;
            ThreadUtility.MemoryBarrier();
        }

        volatile bool _signal;
        volatile object _return;
    }
}