#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    //StaggeredMonoRunner runs not more than maxTasksPerIteration tasks in one single iteration.
    //Several tasks must run on this runner to make sense. TaskCollections are considered
    //single tasks, so they don't count (may change in future)
    /// </summary>
    public class StaggeredMonoRunner : MonoRunner
    {
        public StaggeredMonoRunner(string name, int maxTasksPerIteration, bool mustSurvive = false)
        {
            _flushingOperation = new UnityCoroutineRunner.FlushingOperation();
            
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            var info = new StaggeredRunningInfo(maxTasksPerIteration) { runnerName = name };

            runnerBehaviour.StartUpdateCoroutine(new UnityCoroutineRunner.Process
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }

        class StaggeredRunningInfo : UnityCoroutineRunner.RunningTasksInfo
        {
            public StaggeredRunningInfo(float maxTasksPerIteration)
            {
                _maxTasksPerIteration = maxTasksPerIteration;
            }
            
            public override bool CanMoveNext(ref int index, int count, object current)
            {
                if (_iterations >= _maxTasksPerIteration)
                {
                    _iterations = 0;

                    return false;
                }

                _iterations++;

                return true;
            }

            public override void Reset()
            {
                _iterations = 0;
            }

            int _iterations;
            readonly float _maxTasksPerIteration;
        }
    }
}
#endif