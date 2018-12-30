#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using Svelto.Common;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    /// while you can instantiate a BaseRunner, you should use the standard one whenever possible. Instantiating
    /// multiple runners will defeat the initial purpose to get away from the Unity monobehaviours internal updates.
    /// MonoRunners are disposable though, so at least be sure to dispose the ones that are unused
    /// CoroutineMonoRunner is the only Unity based Svelto.Tasks runner that can handle Unity YieldInstructions
    /// You should use YieldInstructions only when extremely necessary as often an Svelto.Tasks IEnumerator
    /// replacement is available.
    /// </summary>
    public class ExtraLeanCoroutineMonoRunner<T> : UpdateMonoRunner<ExtraLeanSveltoTask<T>> where 
        T:IEnumerator
    {
        public ExtraLeanCoroutineMonoRunner(string name) : base(name)
        {
        }
    }
    
    public class LeanCoroutineMonoRunner<T> : UpdateMonoRunner<LeanSveltoTask<T>> where T:IEnumerator<TaskContract>
    {
        public LeanCoroutineMonoRunner(string name) : base(name)
        {
        }
    }
    
    public class CoroutineMonoRunner<T> : CoroutineMonoRunner<T, StandardRunningTasksInfo> where T : ISveltoTask
    {
        public CoroutineMonoRunner(string name) : base(name, new StandardRunningTasksInfo())
        {
        }
    }
    
    public class CoroutineMonoRunner<T, TFlowModifier> : BaseRunner<T> where T: ISveltoTask
                                                                       where TFlowModifier:IRunningTasksInfo
    {
        public CoroutineMonoRunner(string name, TFlowModifier modifier):base(name)
        {
            modifier.runnerName = name;
            
            _processEnumerator =
                new CoroutineRunner<T>.Process<TFlowModifier, PlatformProfiler>
                (_newTaskRoutines, _coroutines, _flushingOperation, modifier);
            
            UnityCoroutineRunner.StartCoroutine(_processEnumerator);
        }

        public void StartYieldInstruction(IEnumerator instruction)
        {
            UnityCoroutineRunner.StartYieldCoroutine(instruction);
        }
    }
}
#endif
