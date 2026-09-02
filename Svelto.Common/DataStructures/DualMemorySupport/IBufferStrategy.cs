using System;
using Svelto.Common;

namespace Svelto.DataStructures
{
    public interface IBufferStrategy<T>
    {
        int       capacity           { get; }
        bool      isValid            { get; }

        void   Alloc(uint size, Allocator allocator, bool memClear = true);
        void   ShiftRight(uint index, uint count);
        void   ShiftLeft(uint index, uint count);
        void   Resize(uint newCapacity, bool copyContent = true, bool memClear = true);
        IntPtr AsBytesPointer();
        void   SerialiseFrom(IntPtr bytesPointer);
        void   Clear();
        void   FastClear();
        
        ref T this[uint index] { get ; }
        ref T this[int index] { get ; }
        
        IBuffer<T> ToBuffer();

        void Dispose();
    }
}