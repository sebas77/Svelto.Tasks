using System.Collections;

namespace Svelto.Tasks.Internal
{
    class ParallelRunEnumerator<T> : IEnumerator where T:IMultiThreadParallelizable
    {
        public ParallelRunEnumerator(ref T job, int startIndex, int numberOfIterations)
        {
            _startIndex = startIndex;
            _numberOfITerations = numberOfIterations;
            _job = job;
        }

        public bool MoveNext()
        {
            _endIndex = _startIndex + _numberOfITerations;

            Loop();

            return false;
        }

        void Loop()
        {
            for (_index = _startIndex; _index < _endIndex; _index++)
                _job.Update(_index);
        }

        public void Reset()
        {}

        public object Current { get; }
        
        int _startIndex;
        int _numberOfITerations;
        int _index;
        int _endIndex;
        
        T _job;
    }
}