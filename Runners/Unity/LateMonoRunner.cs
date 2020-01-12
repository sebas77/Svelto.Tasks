#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    public class LateMonoRunner: LateMonoRunner<IEnumerator>
    {
        public LateMonoRunner(string name) : base(name)
        {
        }
    }
    public class LateMonoRunner<T> : MonoRunner<T> where T:IEnumerator
    {
        public LateMonoRunner(string name):base(name)
        {
            var info = new UnityCoroutineRunner<T>.RunningTasksInfo() { runnerName = name };

            UnityCoroutineRunner<T>.StartLateCoroutine(new UnityCoroutineRunner<T>.Process<UnityCoroutineRunner<T>.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }
    }
}
#endif