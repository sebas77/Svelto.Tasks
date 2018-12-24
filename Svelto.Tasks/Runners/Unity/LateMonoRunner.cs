#if later
#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    public class LateMonoRunner: LateMonoRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
    {
        public LateMonoRunner(string name) : base(name)
        {
        }
    }
    public class LateMonoRunner<T> : BaseRunner<T> where T: ISveltoTask
    {
        public LateMonoRunner(string name):base(name)
        {
            var info = new UnityCoroutineRunner<T>.RunningTasksInfo() { runnerName = name };

            StartProcess(new UnityCoroutineRunner<T>.Process<UnityCoroutineRunner<T>.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }
    }
}
#endif
#endif