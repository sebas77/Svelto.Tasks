using System;
using System.Collections;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.Enumerators
{
    public readonly struct Continuation: IEnumerator
    {
        readonly ContinuationEnumeratorInternal ce;
#if DEBUG && !PROFILE_SVELTO
        internal readonly WeakReference<IRunner> _runner;
#endif
        internal Continuation(ContinuationEnumeratorInternal continuation) : this()
        {
            _signature = continuation.signature;
            ce         = continuation;
        }

#if DEBUG && !PROFILE_SVELTO
        internal Continuation(ContinuationEnumeratorInternal continuation, IRunner runner)
        {
            _signature = continuation.signature;
            ce         = continuation;
            _runner    = new WeakReference<IRunner>(runner);
        }
#endif

        public bool isRunning => ce.MoveNext(_signature);

        readonly DateTime _signature;
        
        public   bool     MoveNext()
        {
            return ce.MoveNext(_signature);
        }

        public   void     Reset()
        {
            throw new NotImplementedException();
        }

        public object Current => Yield.It;

        public void ReturnToPool()
        {
            ce?.ReturnToPool();
        }
    }

    /// <summary>
    /// The Continuation Wrapper contains a valid value until the task is not stopped. After that it should be released. 
    /// </summary>
    class ContinuationEnumeratorInternal
    {
        internal ContinuationEnumeratorInternal()
        {
            signature = DateTime.Now;
        }

        public bool MoveNext(in DateTime signature)
        {
            return signature == this.signature;
        }

        internal void ReturnToPool()
        {
            Reset();
            //careful, this reasoning is convoluted:
            //I need to be sure that the ContinuatorEnumerator is invalid in the moment is back to the pool
            //(it would be the same to shift the reasoning when it's take from the pool, but this is even safer)
            //At this point in time, Svelto.Tasks may still holding the continuation enumerator to check if the
            //task is done. But how can I know how long a runner is going to hold the continuation enumerator for?
            //therefore the "signature" will invalidate stale holders and therefore it's safe here to set
            //_completed to false

            ContinuationPool.PushBack(this); //and return to the pool
        }

        void Reset()
        {
            signature = DateTime.Now; //invalidate ContinuationEnumerator holding this object            
        }

        ~ContinuationEnumeratorInternal()
        {
            ReturnToPool();
        }

        public DateTime signature { get; private set; }
    }
}