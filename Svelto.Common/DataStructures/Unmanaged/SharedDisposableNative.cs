using System;
using System.Runtime.CompilerServices;
using Svelto.Common;

namespace Svelto.DataStructures
{
    public struct SharedDisposableNative<T> : IDisposable where T : unmanaged, IDisposable
    {
#if UNITY_COLLECTIONS || (UNITY_JOBS || UNITY_BURST)
        [global::Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
#endif        
        unsafe IntPtr ptr;

        public SharedDisposableNative(in T value)
        {
            unsafe
            {
                ptr = MemoryUtilities.NativeAlloc<T>(1, Allocator.Persistent);
                Unsafe.Write((void*)ptr, value);
            }
        }

        public void Dispose()
        {
            unsafe
            {
                Unsafe.AsRef<T>((void*)ptr).Dispose();
                
                MemoryUtilities.NativeFree((IntPtr)ptr, Allocator.Persistent);
                ptr = IntPtr.Zero;
            }
        }

        public ref T value
        {
            get
            {
                unsafe
                {
                    DBC.Common.Check.Require(ptr != null, "SharedNative has not been initialized");

                    return ref Unsafe.AsRef<T>((void*)ptr);
                }
            }
        }
    }
}