using Svelto.Tasks.Unity.Internal;

#if UNITY_5 || UNITY_5_3_OR_NEWER

namespace Svelto.Tasks.Unity
{
    public class UpdateMonoRunner : MonoRunner
    {
        public UpdateMonoRunner(string name, bool mustSurvive = false):base(name)
        {
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            
            var info = new UnityCoroutineRunner.RunningTasksInfo { runnerName = name };

            runnerBehaviour.StartUpdateCoroutine(new UnityCoroutineRunner.Process<UnityCoroutineRunner.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }
    }
}
#endif