#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    public class EndOfFrameRunner : MonoRunner
    {
        public EndOfFrameRunner(string name, bool mustSurvive = false)
        {
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourEndOfFrame>();
            var runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();
            var info = new UnityCoroutineRunner.StandardRunningTaskInfo() { runnerName = name };

            runnerBehaviour.StartEndOfFrameCoroutine(new UnityCoroutineRunner.Process
                (_newTaskRoutines, _coroutines, _flushingOperation, info,
                 UnityCoroutineRunner.StandardTasksFlushing,
                 runnerBehaviourForUnityCoroutine, StartCoroutine));
        }

        int _numberOfRunningTasks;
    }
}
#endif