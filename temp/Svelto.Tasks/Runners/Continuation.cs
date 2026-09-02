using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.Enumerators
{
    public readonly struct Continuation
    {
        internal Continuation(ContinuationEnumeratorInternal continuation) : this()
        {
            _signature = continuation.signature;
            _ce         = continuation;
        }

#if DEBUG && !PROFILE_SVELTO
        internal Continuation(ContinuationEnumeratorInternal continuation, IRunner runner)
        {
            _signature = continuation.signature;
            _ce         = continuation;
            _runner    = new WeakReference<IRunner>(runner);
        }
#endif
        public bool isRunning
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _ce.IsRunning(_signature);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReturnToPool()
        {
            _ce?.ReturnToPool();
        }
        
        readonly long _signature;
        readonly ContinuationEnumeratorInternal _ce;

#if DEBUG && !PROFILE_SVELTO
        internal readonly WeakReference<IRunner> _runner;
#endif
    }

    /// <summary>
    /// The Continuation Wrapper contains a valid value until the task is not stopped. After that it should be released. 
    /// </summary>
    class ContinuationEnumeratorInternal
    {
        internal ContinuationEnumeratorInternal()
        {
            _signature = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRunning(in long signature)
        {
            return signature == Volatile.Read(ref _signature);
        }

        internal void ReturnToPool()
        {
            Reset();
            //careful, this reasoning is convoluted:
            //I need to be sure that the ContinuatorEnumerator is invalid on the moment is back to the pool
            //(it would be the same to shift the reasoning when it's take from the pool, but this is even safer)
            //At this point in time, Svelto.Tasks may still be holding the continuation enumerator to check if the
            //task is done. But how can I know how long a runner is going to hold the continuation enumerator for?
            //therefore the "signature" will invalidate stale holders and therefore it's safe here to set
            //_completed to false
            ContinuationPool.PushBack(this); //and return to the pool
            
            GC.SuppressFinalize(this);
        }

        void Reset()
        {
            // A clock value can repeat when an object is returned and retrieved in the same tick.
            // A generation token must change for every return so stale continuations cannot remain live.
            Interlocked.Increment(ref _signature);
        }

        ~ContinuationEnumeratorInternal()
        {
            ReturnToPool();
        }

        public long signature => Volatile.Read(ref _signature);

        long _signature;
    }
}
