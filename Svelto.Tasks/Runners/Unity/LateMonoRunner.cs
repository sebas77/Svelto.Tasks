#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    public class LateMonoRunner : MonoRunner
    {
        public LateMonoRunner(string name, bool mustSurvive = false):base(name)
        {
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourLate>();
            var info = new UnityCoroutineRunner.RunningTasksInfo() { runnerName = name };

            runnerBehaviour.StartLateCoroutine(new UnityCoroutineRunner.Process<UnityCoroutineRunner.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }
    }
}
#endif