#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    public class UpdateMonoRunner : UpdateMonoRunner<IEnumerator>
    {
        public UpdateMonoRunner(string name) : base(name)
        {
        }
    }
    public class UpdateMonoRunner<T> : MonoRunner<T> where T:IEnumerator
    {
        UnityCoroutineRunner<T>.Process<UnityCoroutineRunner<T>.RunningTasksInfo> enumerator;

        public UpdateMonoRunner(string name):base(name)
        {
            var info = new UnityCoroutineRunner<T>.RunningTasksInfo { runnerName = name };

            enumerator = new UnityCoroutineRunner<T>.Process<UnityCoroutineRunner<T>.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info);
            UnityCoroutineRunner<T>.StartUpdateCoroutine(enumerator);
        }

        public void Step()
        {
            enumerator.MoveNext();
        }
    }
}
#endif