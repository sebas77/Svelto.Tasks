#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    public class EndOfFrameRunner : EndOfFrameRunner<IEnumerator>
    {
        public EndOfFrameRunner(string name) : base(name)
        {
        }
    }
    public class EndOfFrameRunner<T> : MonoRunner<T> where T:IEnumerator
    {
        public EndOfFrameRunner(string name):base(name)
        {
            var info = new UnityCoroutineRunner<T>.RunningTasksInfo() { runnerName = name };

            UnityCoroutineRunner<T>.StartEndOfFrameCoroutine(new UnityCoroutineRunner<T>.Process<UnityCoroutineRunner<T>.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }

        int _numberOfRunningTasks;
    }
}
#endif