using System.Runtime.CompilerServices;

namespace Svelto.Tasks.Parallelism.Internal
{
    public struct ParallelRunEnumerator<T> : IParallelTask where T:struct, ISveltoJob
    {
        public ParallelRunEnumerator(in T job, int startIndex, int numberOfIterations)
        {
            _startIndex = startIndex;
            _numberOfIterations = numberOfIterations;
            _job = job;
            _index = 0;
            _endIndex = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _endIndex = _startIndex + _numberOfIterations;

            Loop();

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Loop()
        {
            for (_index = _startIndex; _index < _endIndex; _index++)
                _job.Update(_index);
        }

        public void Reset()
        {}
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            _job.Dispose();
        }

        public object Current => null;

        readonly int _startIndex;
        readonly int _numberOfIterations;
        T _job;
        
        int _index;
        int _endIndex;
    }
}