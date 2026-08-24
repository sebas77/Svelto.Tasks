using System.Runtime.CompilerServices;

namespace Svelto.DataStructures
{
    public readonly struct FasterReadOnlyList<T> 
    {
        public static FasterReadOnlyList<T> DefaultEmptyList = new FasterReadOnlyList<T>(new FasterList<T>(0));

        public int  count    => _list.count;
        
        public FasterReadOnlyList(FasterList<T> list)
        {
            _list = list;
        }
        
        public static implicit operator FasterReadOnlyList<T>(FasterList<T> list)
        {
            return new FasterReadOnlyList<T>(list);
        }
        
        public static implicit operator LocalFasterReadOnlyList<T>(FasterReadOnlyList<T> list)
        {
            return new LocalFasterReadOnlyList<T>(list._list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FasterListEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }
        
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _list[index];
        }

        public ref T this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _list[(int) index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ToArrayFast(out int count)
        {
            return _list.ToArrayFast(out count);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        internal readonly FasterList<T> _list;
    }
}