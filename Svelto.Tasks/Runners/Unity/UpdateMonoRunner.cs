#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks.Internal.Unity;
    
namespace Svelto.Tasks.Unity
{
    public class UpdateMonoRunner : MonoRunner
    {
        public UpdateMonoRunner(string name, bool mustSurvive = false)
        {
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            var runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();

            var info = new UnityCoroutineRunner.RunningTasksInfo { runnerName = name };

            runnerBehaviour.StartUpdateCoroutine(new UnityCoroutineRunner.Process
                (_newTaskRoutines, _coroutines, _flushingOperation, info,
                 UnityCoroutineRunner.StandardTasksFlushing,
                 runnerBehaviourForUnityCoroutine, StartCoroutine));
        }
    }
}
#endif