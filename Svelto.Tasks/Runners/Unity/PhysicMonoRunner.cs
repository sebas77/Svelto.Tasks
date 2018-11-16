#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    /// while you can istantiate a MonoRunner, you should use the standard one whenever possible. Istantiating multiple
    /// runners will defeat the initial purpose to get away from the Unity monobehaviours internal updates.
    /// MonoRunners are disposable though, so at least be sure to dispose of them once done
    /// </summary>
    public class PhysicMonoRunner : MonoRunner
    {
        public PhysicMonoRunner(string name, bool mustSurvive = false):base(name)
        {
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourPhysic>();
            var info = new UnityCoroutineRunner.RunningTasksInfo() { runnerName = name };

            runnerBehaviour.StartPhysicCoroutine(new UnityCoroutineRunner.Process<UnityCoroutineRunner.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }
    }
}
#endif