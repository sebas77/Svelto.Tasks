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
            var endIndex = _startIndex + _numberOfITerations;

            int i;
            for (i = _startIndex; i < endIndex; i += 8)
            {
                _job.Update(i);
                _job.Update(i + 1);
                _job.Update(i + 2);
                _job.Update(i + 3);
                _job.Update(i + 4);
                _job.Update(i + 5);
                _job.Update(i + 6);
                _job.Update(i + 7);
            }
            
            var count = _numberOfITerations % 8;

            i -= 8;
            for (int j = i; j < i + count; j++)
                _job.Update(j);

            return false;
        }

        public void Reset()
        {}

        public object Current { get; }
        
        int _startIndex;
        int _numberOfITerations;
        T _job;
    }
}