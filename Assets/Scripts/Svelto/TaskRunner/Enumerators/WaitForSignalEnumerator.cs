using System;
using System.Collections;

namespace Svelto.ECS.Example.Parallelism
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
            Tasks.MultiThreadRunner.MemoryBarrier();
            return !_signal;
        }

        public void Reset()
        {
            _signal = false;
            _return = null;
            Tasks.MultiThreadRunner.MemoryBarrier();
        }

        internal void Signal()
        {
            _signal = true;
            Tasks.MultiThreadRunner.MemoryBarrier();
        }

        internal void Signal(object obj)
        {
            _signal = true;
            _return = obj;
            Tasks.MultiThreadRunner.MemoryBarrier();
        }

        volatile bool _signal;
        volatile object _return;
    }
}