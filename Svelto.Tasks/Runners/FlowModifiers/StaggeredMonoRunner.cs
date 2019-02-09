using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    /// <summary>
    /// StaggeredMonoRunner runs not more than maxTasksPerIteration tasks in one single iteration.
    /// Several tasks must run on this runner to make sense. TaskCollections are considered
    /// single tasks, so they don't count (may change in future)
    /// </summary>
    public struct StaggeredRunningInfo : IRunningTasksInfo
    {
        public StaggeredRunningInfo(int maxTasksPerIteration)
        {
            _maxTasksPerIteration = maxTasksPerIteration;
            _iterations           = 0;
            runnerName            = null;
        }

        public bool CanMoveNext(ref int nextIndex, TaskContract currentResult, int coroutinesCount)
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