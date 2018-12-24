#if later
#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    /// StaggeredMonoRunner runs not more than maxTasksPerIteration tasks in one single iteration.
    /// Several tasks must run on this runner to make sense. TaskCollections are considered
    /// single tasks, so they don't count (may change in future)
    /// </summary>
    public class StaggeredMonoRunner : StaggeredMonoRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
    {
        public StaggeredMonoRunner(string name, int maxTasksPerIteration) : base(name, maxTasksPerIteration)
        {
        }
    }
    public class StaggeredMonoRunner<T> : BaseRunner<T> where T: ISveltoTask
    {
        public StaggeredMonoRunner(string name, int maxTasksPerIteration):base(name)
        {
            _flushingOperation = new UnityCoroutineRunner<T>.FlushingOperation();
            
            var info = new StaggeredRunningInfo(maxTasksPerIteration) { runnerName = name };

            StartProcess(new UnityCoroutineRunner<T>.Process<StaggeredRunningInfo>(_newTaskRoutines, 
                                _coroutines, _flushingOperation, info));
        }
        
        struct StaggeredRunningInfo : IRunningTasksInfo
        {
            public StaggeredRunningInfo(int maxTasksPerIteration)
            {
                _maxTasksPerIteration = maxTasksPerIteration;
                _iterations = 0;
                runnerName = "StaggeredRunningInfo";
            }
            
            public bool CanMoveNext(ref int nextIndex, TaskContract currentResult)
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
#endif