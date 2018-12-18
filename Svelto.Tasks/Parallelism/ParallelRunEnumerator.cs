using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Unity;

namespace Svelto.Tasks.Parallelism.Internal
{
    class ParallelRunEnumerator<T> : IEnumerator<TaskContract?> where T:struct, IMultiThreadParallelizable
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

        TaskContract? IEnumerator<TaskContract?>.Current
        {
            get { return null; }
        }

        public object Current
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