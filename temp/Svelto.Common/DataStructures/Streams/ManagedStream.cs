#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER
using System;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures
{
    public struct ManagedStream
    {
        public ManagedStream(byte[] ptr, int capacity):this()
        {
            _ptr = ptr;
            _sveltoStream = new SveltoStream(capacity);
            _offset = 0;
        }

        public ManagedStream(ArraySegment<byte> updateMessage)
        {
            _ptr = updateMessage.Array;
            _sveltoStream = new SveltoStream(updateMessage.Count);
            _offset = updateMessage.Offset;
        }

        public int Length => _sveltoStream.length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>() where T : unmanaged => _sveltoStream.Read<T>(AsSpanInternal());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>(in T item) where T : unmanaged => _sveltoStream.Read<T>(AsSpanInternal());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //T can contain managed elements, it's up to the user to be sure that the right data is read
        public void UnsafeRead<T>(ref T item, int unmanagedStructSize) where T : struct => _sveltoStream.UnsafeRead(ref item, AsSpanInternal(), unmanagedStructSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeRead<T>(ref T item, int unmanagedStructSize, int destOffset) where T : struct => _sveltoStream.UnsafeRead(ref item, AsSpanInternal(), unmanagedStructSize, destOffset);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> ReadSpan<T>() where T : unmanaged => _sveltoStream.ReadSpan<T>(AsSpanInternal());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(in T value) where T : unmanaged => _sveltoStream.Write(AsSpanInternal(), value);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OverwriteAt<T>(in T value, uint cursor)where T : unmanaged => _sveltoStream.OverwriteAt(AsSpanInternal(), value, cursor);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpan<T>(Span<T> valueSpan) where T : unmanaged => _sveltoStream.WriteSpan(AsSpanInternal(), valueSpan);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => _sveltoStream.Clear();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() => _sveltoStream.Reset();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanAdvance() => _sveltoStream.CanAdvance();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanAdvance(int elementSize) => _sveltoStream.CanAdvance(elementSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanAdvance<T>() where T : unmanaged => _sveltoStream.CanAdvance<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan() => new(_ptr, _offset, _sveltoStream.length); //returns what has been written so far in the buffer

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<byte> AsMemory() => new(_ptr, _offset, _sveltoStream.length); //returns what has been written so far in the buffer

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> AsArraySegment() => new(_ptr, _offset, _sveltoStream.length); //returns what has been written so far in the buffer
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AdvanceCursor(int sizeOf) => _sveltoStream.AdvanceCursor(sizeOf);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Memory<byte> AsMemoryInternal() => new(_ptr, _offset, _sveltoStream.capacity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Span<byte> AsSpanInternal() => new (_ptr, _offset, _sveltoStream.capacity);

        SveltoStream _sveltoStream; //CANNOT BE READ ONLY

        readonly byte[] _ptr;
        readonly int _offset;
    }
}
#endif
