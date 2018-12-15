#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    class EarlyUpdateMonoRunner : EarlyUpdateMonoRunner<IEnumerator>
    {
        public EarlyUpdateMonoRunner(string name) : base(name)
        {
        }
    }
    class EarlyUpdateMonoRunner<T> : MonoRunner<T> where T:IEnumerator
    {
        public EarlyUpdateMonoRunner(string name):base(name)
        {
            var info = new UnityCoroutineRunner<T>.RunningTasksInfo() { runnerName = name };

            UnityCoroutineRunner<T>.StartEarlyUpdateCoroutine(new UnityCoroutineRunner<T>.Process<UnityCoroutineRunner<T>.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }
    }
}
#endif
