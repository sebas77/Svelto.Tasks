#if MUST_BE_REWRITTEN
#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks.Unity.Internal;

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
#endif