using System.Runtime.InteropServices;

namespace Svelto.DataStructures
{
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct ValueIndex
    {
        internal uint sparseIndex => _sparseIndex & 0x00FFFFFF;
        internal byte version => _version;

        [FieldOffset(0)] readonly uint _sparseIndex;
        [FieldOffset(3)] readonly byte _version;

        public ValueIndex(uint index, byte ver)
        {
            _sparseIndex = index;
            _version = ver;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct SparseIndex
    {
        internal uint denseIndex => _denseIndex & 0x00FFFFFF;
        internal byte version => _version;

        [FieldOffset(0)] readonly uint _denseIndex;
        [FieldOffset(3)] readonly byte _version;
        [FieldOffset(4)] readonly uint _nextFreeIndex;
        [FieldOffset(7)] readonly byte _nextFreeVer;

        public SparseIndex(uint index, byte ver):this()
        {
            _denseIndex = index;
            _version = ver;
        }

        internal SparseIndex(SparseIndex index, SparseIndex nextFree)
        {
            _denseIndex = index.denseIndex;
            _version = (byte)(index.version + 1);
            _nextFreeIndex = nextFree.denseIndex;
            _nextFreeVer = nextFree._version;
        }

        public bool IsValid()
        {
            return _version > 0;
        }

        public SparseIndex Next()
        {
            return new SparseIndex(_nextFreeIndex, _nextFreeVer);
        }
    }
}
