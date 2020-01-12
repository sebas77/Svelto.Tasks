#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    /// StaggeredMonoRunner runs not more than maxTasksPerIteration tasks in one single iteration.
    /// Several tasks must run on this runner to make sense. TaskCollections are considered
    /// single tasks, so they don't count (may change in future)
    /// </summary>
    public class StaggeredMonoRunner : StaggeredMonoRunner<IEnumerator>
    {
        public StaggeredMonoRunner(string name, int maxTasksPerIteration) : base(name, maxTasksPerIteration)
        {
        }
    }
    public class StaggeredMonoRunner<T> : MonoRunner<T> where T:IEnumerator
    {
        UnityCoroutineRunner<T>.Process<StaggeredMonoRunner<T>.StaggeredRunningInfo> enumerator;

        public StaggeredMonoRunner(string name, int maxTasksPerIteration):base(name)
        {
            _flushingOperation = new UnityCoroutineRunner<T>.FlushingOperation();
            
            var info = new StaggeredRunningInfo(maxTasksPerIteration) { runnerName = name };

            enumerator = new UnityCoroutineRunner<T>.Process<StaggeredRunningInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info);
            UnityCoroutineRunner<T>.StartUpdateCoroutine(enumerator);
        }
        
        public void Step()
        {
            enumerator.MoveNext();
        }

        struct StaggeredRunningInfo : IRunningTasksInfo<T>
        {
            public StaggeredRunningInfo(int maxTasksPerIteration)
            {
                _maxTasksPerIteration = maxTasksPerIteration;
                _iterations = 0;
                runnerName = "StaggeredRunningInfo";
            }
            
            public bool CanMoveNext(ref int nextIndex, TaskCollection<T>.CollectionTask currentResult)
            {
                if (_iterations >= _maxTasksPerIteration - 1)
                {
                    _iterations = 0;

                    return false;
                }

                _iterations++;

                return true;
            }

            public bool CanProcessThis(ref int index)
            {
                return true;
            }

            public void Reset()
            {
                _iterations = 0;
            }

            public string runnerName { get; set; }

            int            _iterations;
            readonly float _maxTasksPerIteration;
        }
    }
}
#endif