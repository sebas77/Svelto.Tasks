#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Svelto.DataStructures
{
    public static class FasterListExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> ToByteSpan<T>(this FasterList<T> list) where T : unmanaged
        {
            T[] array = list.ToArrayFast(out var count);

            Span<T> spanT = array.AsSpan(0, count);

            Span<byte> span = MemoryMarshal.AsBytes(spanT);
            return span;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ToSpan<T>(this FasterList<T> list) where T : unmanaged
        {
            T[] array = list.ToArrayFast(out var count);

            Span<T> spanT = array.AsSpan(0, count);

            return spanT;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this FasterList<T> to, IList<T> from)
            where T : unmanaged
        {
            to.SetCountTo((uint)from.Count);
            var destArray = to.ToArrayFast(out _);
            from.CopyTo(destArray, 0);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this FasterList<T> to, T[] from)
            where T : unmanaged
        {
            to.SetCountTo((uint)from.Length);
            var destArray = to.ToArrayFast(out _);
            from.CopyTo(destArray, 0);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this FasterList<T> to, FasterList<T> from)
            where T : unmanaged
        {
            to.SetCountTo((uint)from.count);
            var destArray = to.ToArrayFast(out _);
            from.CopyTo(destArray, 0);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this FasterList<T> to, FasterReadOnlyList<T> from)
            where T : unmanaged
        {
            to.SetCountTo((uint)from.count);
            var destArray = to.ToArrayFast(out _);
            from.CopyTo(destArray, 0);
        }

#if IF_THE_OTHER_SOLUTION_FAILS
        internal readonly ref struct DisposableHandle
        {
            public DisposableHandle(GCHandle gcHandle)
            {
                _gcHandle = gcHandle;
            }

            public void Dispose()
            {
                _gcHandle.Free();
            }

            readonly GCHandle _gcHandle;
        }

       public static DisposableHandle ToSpan<T>(this FasterList<T> list, out ReadOnlySpan<byte> readOnlySpan)
            where T : unmanaged
        {
            unsafe
            {
                T[] array = list.ToArrayFast(out var count);

                var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                var intPtr = handle.AddrOfPinnedObject();
                var sizeOf = UnsafeUtility.SizeOf<T>();

                readOnlySpan = new ReadOnlySpan<byte>((void*)intPtr, count * sizeOf);

                return new DisposableHandle(handle);
            }
        }
#endif
    }
}
#endif