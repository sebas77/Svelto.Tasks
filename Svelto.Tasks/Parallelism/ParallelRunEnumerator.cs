using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks.Parallelism.Internal
{
    public class ParallelRunEnumerator<T> : IEnumerator<TaskContract> where T:struct, ISveltoJob
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

        public TaskContract Current
        {
            get { return Yield.It; }
        }

        object IEnumerator.Current
        {
            get { return null; }
        }

        readonly int _startIndex;
        readonly int _numberOfITerations;
        readonly T _job;
        
        int _index;
        int _endIndex;
        
        public void Dispose()
        {
        }
    }
}