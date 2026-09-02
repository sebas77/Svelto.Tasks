#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER
using System;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures
{
    public struct UnmanagedStream
    {
        public unsafe UnmanagedStream(byte* ptr, int sizeInByte):this()
        {
            _ptr = ptr;
            _sveltoStream = new SveltoStream(sizeInByte);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>() where T : unmanaged => _sveltoStream.Read<T>(AsSpanInternal());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(in T value) where T : unmanaged => _sveltoStream.Write(AsSpanInternal(), value);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //T can contain managed elements, it's up to the user to be sure that the right data is read
        public void UnsafeWrite<T>(in T value, int unmanagedStructSize) where T : struct => _sveltoStream.UnsafeWrite(AsSpanInternal(), value, unmanagedStructSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpan<T>(Span<T> valueSpan) where T : unmanaged => _sveltoStream.WriteSpan(AsSpanInternal(), valueSpan);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => _sveltoStream.Clear();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() => _sveltoStream.Reset();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanAdvance() => _sveltoStream.CanAdvance();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan() //returns what has been written so far in the buffer
        {
            unsafe
            {
                return new Span<byte>(_ptr, _sveltoStream.length);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Span<byte> AsSpanInternal()
        {
            unsafe
            {
                return new Span<byte>(_ptr, _sveltoStream.capacity);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AdvanceCursor(int sizeOf) => _sveltoStream.AdvanceCursor(sizeOf);

        SveltoStream _sveltoStream; //CANNOT BE READ ONLY
#if UNITY_COLLECTIONS || UNITY_JOBS || UNITY_BURST    
#if UNITY_BURST
        [Unity.Burst.NoAlias]
#endif
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
#endif
        readonly unsafe byte* _ptr;
    }
}
#endif
