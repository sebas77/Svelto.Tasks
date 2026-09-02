#if UNITY_COLLECTIONS
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Svelto.DataStructures
{
    public static class NativeDynamicArrayUnityExtension
    {
        public static NativeArray<T> ToNativeArray<T>(this in NativeDynamicArray array) where T : struct
        {
            unsafe
            {
                var nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
                    (void*) array.ToIntPTR<T>(), (int) array.Count<T>(), (Unity.Collections.Allocator)Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, AtomicSafetyHandle.Create());
#endif
                return nativeArray;
            }
        }
    }
}
#endif