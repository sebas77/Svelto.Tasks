#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    public class EndOfFrameRunner : MonoRunner
    {
        public EndOfFrameRunner(string name, bool mustSurvive = false):base(name)
        {
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourEndOfFrame>();
            var info = new UnityCoroutineRunner.RunningTasksInfo() { runnerName = name };

            runnerBehaviour.StartEndOfFrameCoroutine(new UnityCoroutineRunner.Process<UnityCoroutineRunner.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }

        int _numberOfRunningTasks;
    }
}
#endif