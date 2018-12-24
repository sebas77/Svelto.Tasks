#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    public class ExtraLeanUpdateMonoRunner<T> : UpdateMonoRunner<ExtraLeanSveltoTask<T>> where 
        T:IEnumerator
    {
        public ExtraLeanUpdateMonoRunner(string name) : base(name)
        {
        }
    }
    
    public class LeanUpdateMonoRunner<T> : UpdateMonoRunner<LeanSveltoTask<T>> where T:IEnumerator<TaskContract>
    {
        public LeanUpdateMonoRunner(string name) : base(name)
        {
        }
    }
    
    public class UpdateMonoRunner<T> : BaseRunner<T> where T: ISveltoTask
    {
        public UpdateMonoRunner(string name):base(name)
        {
            var info = new CoroutineRunner<T>.StandardRunningTasksInfo { runnerName = name };

            _processEnumerator = new CoroutineRunner<T>.Process<CoroutineRunner<T>.StandardRunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info);
            
            UnityCoroutineRunner.StartUpdateCoroutine(_processEnumerator);
        }
    }
}
#endif
