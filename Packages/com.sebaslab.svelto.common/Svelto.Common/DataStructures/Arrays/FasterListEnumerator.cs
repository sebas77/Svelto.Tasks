namespace Svelto.DataStructures
{
    public ref struct FasterListEnumerator<T>
    {
        public ref T Current
        {
            get
            {
                DBC.Common.Check.Require(_counter <= _size);
                return ref _buffer[(uint)_counter - 1];
            }
        }

        public FasterListEnumerator(FasterList<T> buffer, uint size)
        {
            _size = size;
            _counter = 0;
            _buffer = buffer;
        }

        public bool MoveNext()
        {
            DBC.Common.Check.Require(_size == _buffer.count, "FasterListEnumerator: the list has been modified during the iteration");
            
            if (_counter++ < _size)
                return true;

            return false;
        }

        public void Reset()
        {
            _counter = 0;
        }

        readonly FasterList<T>  _buffer;
        int           _counter;
        readonly uint _size;
    }
}