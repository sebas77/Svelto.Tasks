#if DEBUG && !PROFILE_SVELTO
#define ENABLE_DEBUG_CHECKS
#endif

using System;
using System.Threading;

namespace Svelto.DataStructures
{
    //A sentinel field must never be readonly and must always use the ENABLE_DEBUG_CHECKS. Using may prevent inline
    public struct Sentinel
    {
        public const int readFlag  = 1;
        public const int writeFlag = 2;

#if ENABLE_DEBUG_CHECKS
#if UNITY_COLLECTIONS || (UNITY_JOBS || UNITY_BURST)
        [global::Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
#endif
        unsafe int* _state;
        int         _flag;
#endif

        public unsafe Sentinel(IntPtr statePTR, int flag) : this()
        {
#if ENABLE_DEBUG_CHECKS
            _state = (int*)statePTR;
            _flag  = flag;
#endif
        }

        public unsafe Sentinel(int* statePTR, int flag) : this()
        {
#if ENABLE_DEBUG_CHECKS
            _state = statePTR;
            _flag  = flag;
#endif
        }

        public TestThreadSafety TestThreadSafety()
        {
#if ENABLE_DEBUG_CHECKS
            return new TestThreadSafety(this);
#else
            return default;
#endif
        }

#if ENABLE_DEBUG_CHECKS
        internal unsafe void Use()
        {
            if (_flag == writeFlag)
            {
                // Writer: only allowed if there are no readers/writers (state == 0)
                if (Interlocked.CompareExchange(ref *_state, -1, 0) != 0)
                    throw new Exception("This datastructure is not thread safe: writer requested while buffer is being read or written");

                return;
            }

            if (_flag == readFlag)
            {
                // Reader: allowed if no writer is active (state != -1)
                while (true)
                {
                    var current = Volatile.Read(ref *_state);
                    if (current == -1)
                        throw new Exception("This datastructure is not thread safe: reader requested while buffer is being written");

                    if (Interlocked.CompareExchange(ref *_state, current + 1, current) == current)
                        return;
                }
            }
        }

        internal unsafe void Release()
        {
            if (_flag == writeFlag)
            {
                Volatile.Write(ref *_state, 0);
                return;
            }

            if (_flag == readFlag)
            {
                Interlocked.Decrement(ref *_state);
            }
        }
#endif
    }

    public struct TestThreadSafety : IDisposable
    {
#if ENABLE_DEBUG_CHECKS
        Sentinel _sentinel;
#endif

        public TestThreadSafety(Sentinel sentinel)
        {
#if ENABLE_DEBUG_CHECKS
            _sentinel = sentinel;
            _sentinel.Use();
#endif
        }

        public void Dispose()
        {
#if ENABLE_DEBUG_CHECKS
            _sentinel.Release();
#endif
        }
    }
}