#if later
#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    /// while you can istantiate a BaseRunner, you should use the standard one whenever possible. Istantiating multiple
    /// runners will defeat the initial purpose to get away from the Unity monobehaviours internal updates.
    /// MonoRunners are disposable though, so at least be sure to dispose of them once done
    /// </summary>
    public class PhysicMonoRunner : PhysicMonoRunner<LeanSveltoTask<IEnumerator<TaskContract>>> 
    {
        public PhysicMonoRunner(string name) : base(name)
        {
        }
    }
    public class PhysicMonoRunner<T> : BaseRunner<T> where T: ISveltoTask
    {
        public PhysicMonoRunner(string name):base(name)
        {
            var info = new UnityCoroutineRunner<T>.RunningTasksInfo() { runnerName = name };

            StartProcess(new UnityCoroutineRunner<T>.Process<UnityCoroutineRunner<T>.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }
    }
}
#endif
#endif