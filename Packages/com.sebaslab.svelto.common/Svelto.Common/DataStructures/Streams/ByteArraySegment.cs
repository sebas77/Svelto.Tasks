#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Svelto.DataStructures
{
    public readonly struct ByteArraySegment<T> where T : unmanaged
    {
        public ByteArraySegment(Memory<byte> reference): this()
        {
            _byteReference = reference;
        }

        public ByteArraySegment(T[] reference): this()
        {
            _reference = reference;
        }

        public static implicit operator ReadOnlySpan<T>(in ByteArraySegment<T> list)
        {
            return list.Span;
        }

        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _reference == null ? MemoryMarshal.Cast<byte, T>(_byteReference.Span) : new Span<T>(_reference);
        }

        readonly Memory<byte> _byteReference;
        readonly T[] _reference;
    }

    public static class ByteArraySegmentExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ByteArraySegment<T> ReadByteArraySegment<T>(this ref ManagedStream stream) where T : unmanaged
        {
            int length = stream.Read<int>();

            int readCursor = stream.AdvanceCursor(length);

            return new ByteArraySegment<T>(stream.AsMemoryInternal().Slice(readCursor, length));
        }
    }
}
#endif