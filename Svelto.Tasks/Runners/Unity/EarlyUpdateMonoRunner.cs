#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks.Internal.Unity;

namespace Svelto.Tasks.Unity
{
    public class EarlyUpdateMonoRunner : MonoRunner
    {
        public EarlyUpdateMonoRunner(UpdateMonoRunner updateRunner, string name)
        {
            _go = updateRunner._go;

            var runnerBehaviour = _go.GetComponent<RunnerBehaviourUpdate>();
            var runnerBehaviourForUnityCoroutine = _go.GetComponent<RunnerBehaviour>();

            var info = new UnityCoroutineRunner.RunningTasksInfo { runnerName = name };

            runnerBehaviour.StartEarlyUpdateCoroutine(new UnityCoroutineRunner.Process
                (_newTaskRoutines, _coroutines, _flushingOperation, info,
                 UnityCoroutineRunner.StandardTasksFlushing,
                 runnerBehaviourForUnityCoroutine, StartCoroutine));
        }
    }
}
#endif