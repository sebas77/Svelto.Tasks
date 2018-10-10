#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.DataStructures;
using Svelto.Tasks.Internal.Unity;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    //StaggeredMonoRunner doesn't flush all the tasks at once, runs maxIterationPerFrame
    /// </summary>
    public class StaggeredMonoRunner : MonoRunner
    {
        public StaggeredMonoRunner(string name, int maxIterationPerFrame, bool mustSurvive = false)
        {
            _flushingOperation = new UnityCoroutineRunner.FlushingOperation();
            
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            var runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();
            var info = new StaggeredRunningInfo(maxIterationPerFrame) { runnerName = name };

            runnerBehaviour.StartUpdateCoroutine(new UnityCoroutineRunner.Process
                (_newTaskRoutines, _coroutines, _flushingOperation, info,
                 UnityCoroutineRunner.StandardTasksFlushing,
                 runnerBehaviourForUnityCoroutine, StartCoroutine));
        }

        class StaggeredRunningInfo : UnityCoroutineRunner.RunningTasksInfo
        {
            public StaggeredRunningInfo(float maxTasksPerFrame)
            {
                _maxTasksPerFrame = maxTasksPerFrame;
            }
            
            public override bool MoveNext(ref int index, int count, object current)
            {
                if (_iterations > _maxTasksPerFrame)
                {
                    _iterations = 0;

                    return false;
                }

                _iterations++;

                return true;
            }

            int _iterations;
            readonly float _maxTasksPerFrame;
        }
    }
}
#endif