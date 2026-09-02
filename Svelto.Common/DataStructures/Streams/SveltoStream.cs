#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Svelto.Common;

namespace Svelto.DataStructures
{
    public struct SveltoStream
    {
        public readonly int capacity;
        public int length { get; private set; }

        public SveltoStream(int sizeInByte): this()
        {
            capacity = sizeInByte;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Read<T>(Span<byte> source) where T : unmanaged
        {
            int currentSize = MemoryUtilities.SizeOf<T>();
            int readCursor = _cursor;

            if (readCursor + currentSize > capacity)
            {
                throw new Exception($"TRYING TO READ PAST THE END OF THE STREAM (cursor: {_cursor}, read size: {currentSize}, capacity: {capacity})!");
            }

            _cursor += currentSize;
            return ref Unsafe.As<byte, T>(ref Unsafe.Add(ref MemoryMarshal.GetReference(source), readCursor));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeRead<T>(ref T item, Span<byte> source, int currentSize) where T : struct
        {
            if (_cursor + currentSize > capacity)
            {
                throw new Exception($"TRYING TO READ PAST THE END OF THE STREAM (cursor: {_cursor}, read size: {currentSize}, capacity: {capacity})!");
            }

#if DEBUG
            if (currentSize > Unsafe.SizeOf<T>())
            {
                throw new Exception("size is bigger than struct");
            }
#endif

            Unsafe.CopyBlockUnaligned(
                ref Unsafe.As<T, byte>(ref item),
                ref Unsafe.Add(ref MemoryMarshal.GetReference(source), _cursor),
                (uint)currentSize); // size is not the size of T

            _cursor += currentSize;
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeRead<T>(ref T item, Span<byte> source, int currentSize, int destOffset)
            where T : struct
        {
            if (_cursor + currentSize > capacity)
                throw new Exception($"TRYING TO READ PAST THE END OF THE STREAM (cursor: {_cursor}, read size: {currentSize}, capacity: {capacity})!");
    
    #if DEBUG
            if (destOffset < 0 || currentSize < 0)
                throw new Exception("invalid offset/size");
    
            if (destOffset + currentSize > Unsafe.SizeOf<T>())
                throw new Exception("read would overflow destination struct");
    #endif
    
            ref byte dst = ref Unsafe.Add(ref Unsafe.As<T, byte>(ref item), destOffset);
            ref byte src = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), _cursor);
    
            Unsafe.CopyBlockUnaligned(ref dst, ref src, (uint)currentSize);
    
            _cursor += currentSize;
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(Span<byte> destinationSpan, in T value) where T : unmanaged
        {
            int size = MemoryUtilities.SizeOf<T>();
            if (_cursor + size > capacity)
            {
                throw new Exception("STREAM DOES NOT HAVE ENOUGH SPACE LEFT TO WRITE -- this is bad!");
            }

            Unsafe.As<byte, T>(ref Unsafe.Add(ref MemoryMarshal.GetReference(destinationSpan), _cursor)) = value;
            _cursor += size;
            length = _cursor;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OverwriteAt<T>(Span<byte> destinationSpan, in T value, uint start) where T : unmanaged
        {
            int size = MemoryUtilities.SizeOf<T>();
            
            if ((start > (uint)length || start + size > length))
                throw new InvalidOperationException("OverwriteAt can only overwrite already-written data.");
        
            Unsafe.As<byte, T>(ref Unsafe.Add(ref MemoryMarshal.GetReference(destinationSpan), start)) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeWrite<T>(Span<byte> destinationSpan, T item, int size) where T : struct
        {
            if (_cursor + size > capacity)
            {
                throw new Exception("STREAM DOES NOT HAVE ENOUGH SPACE LEFT TO WRITE -- this is bad!");
            }

#if DEBUG
            if (size > Unsafe.SizeOf<T>())
            {
                throw new Exception("size is bigger than struct");
            }
#endif
            //T can contain managed elements, it's up to the user to be sure that the right data is written
            //I cannot use span for this reason
            Unsafe.CopyBlockUnaligned(
                ref Unsafe.Add(ref MemoryMarshal.GetReference(destinationSpan), _cursor),
                ref Unsafe.As<T, byte>(ref item), (uint)size);

            _cursor += size;
            length = _cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSpan<T>(Span<byte> destinationSpan, Span<T> valueSpan) where T : unmanaged
        {
            int elementSize = MemoryUtilities.SizeOf<T>();

            // NOTE: we write a USHORT for the size to optimize deltas
            // serialise the length of the span in bytes
            ushort lengthToWrite = (ushort) (elementSize * valueSpan.Length);
            Write(destinationSpan, lengthToWrite);

            if (_cursor + lengthToWrite > capacity)
            {
                throw new Exception("STREAM DOES NOT HAVE ENOUGH SPACE LEFT TO WRITE THE SPAN -- this is bad!");
            }

            if (lengthToWrite > 0)
            {
                // create a local span of the destination from the right offset.
                Span<byte> destination = destinationSpan.Slice(_cursor, lengthToWrite);
                valueSpan.CopyTo(MemoryMarshal.Cast<byte, T>(destination));

                _cursor += lengthToWrite;
                length = _cursor;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> ReadSpan<T>(Span<byte> source) where T : unmanaged
        {
            // Read the byte-length of the span (ushort written by WriteSpan)
            ushort lengthToRead = Read<ushort>(source);
            if (lengthToRead == 0)
                return Span<T>.Empty;
            // Advance cursor by the data length and get start index
            int dataStart = AdvanceCursor(lengthToRead);
            var span = source.Slice(dataStart, lengthToRead);
            return MemoryMarshal.Cast<byte, T>(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _cursor = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _cursor = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanAdvance()
        {
            return _cursor < capacity;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanAdvance(int elementSize)
        {
            return _cursor + elementSize < capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanAdvance<T>() where T : unmanaged
        {
            int elementSize = MemoryUtilities.SizeOf<T>();
            return _cursor + elementSize < capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AdvanceCursor(int currentSize)
        {
            if (_cursor + currentSize > capacity)
            {
                throw new Exception($"TRYING TO ADVANCE THE CURSOR PAST THE END OF THE STREAM (cursor: {_cursor}, read size: {currentSize}, capacity: {capacity}!");
            }

            int readCursor = _cursor;
            _cursor += currentSize;
            return readCursor;
        }

        int _cursor;
    }
}
#endif
