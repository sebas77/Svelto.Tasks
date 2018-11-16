using Svelto.Tasks.Unity.Internal;

#if UNITY_5 || UNITY_5_3_OR_NEWER

namespace Svelto.Tasks.Unity
{
    public class EarlyUpdateMonoRunner : MonoRunner
    {
        public EarlyUpdateMonoRunner(UpdateMonoRunner updateRunner, string name):base(name)
        {
            _go = updateRunner._go;

            var runnerBehaviour = _go.GetComponent<RunnerBehaviourUpdate>();

            var info = new UnityCoroutineRunner.RunningTasksInfo() { runnerName = name };

            runnerBehaviour.StartEarlyUpdateCoroutine(new UnityCoroutineRunner.Process<UnityCoroutineRunner.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }
    }
}
#endif