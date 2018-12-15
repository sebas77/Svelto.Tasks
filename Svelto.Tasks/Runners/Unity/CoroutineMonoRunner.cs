#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.Tasks.Unity.Internal;
using UnityEngine;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    /// while you can instantiate a MonoRunner, you should use the standard one whenever possible. Instantiating
    /// multiple runners will defeat the initial purpose to get away from the Unity monobehaviours internal updates.
    /// MonoRunners are disposable though, so at least be sure to dispose the ones that are unused
    /// CoroutineMonoRunner is the only Unity based Svelto.Tasks runner that can handle Unity YieldInstructions
    /// You should use YieldInstructions only when extremely necessary as often an Svelto.Tasks IEnumerator
    /// replacement is available.
    /// </summary>
    public class CoroutineMonoRunner : CoroutineMonoRunner<IEnumerator>
    {
        public CoroutineMonoRunner(string name) : base(name)
        {
        }
    }
    public class CoroutineMonoRunner<T> : MonoRunner<T> where T:IEnumerator
    {
        public CoroutineMonoRunner(string name):base(name)
        {
            var info = new UnityCoroutineRunner<T>.RunningTasksInfo { runnerName = name };

            _processEnumerator = new UnityCoroutineRunner<T>.Process<UnityCoroutineRunner<T>.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info);
            
            UnityCoroutineRunner<T>.StartCoroutine(_processEnumerator);
        }
        
        public override void StartCoroutine(ISveltoTask<T> task)
        {
            isPaused = false;

            _newTaskRoutines.Enqueue(task);
            _flushingOperation.immediate = true;
            _processEnumerator.MoveNext();
            _flushingOperation.immediate = false;
        }
        
        public void StartYieldInstruction(IEnumerator instruction)
        {
            UnityCoroutineRunner<T>.StartCoroutine(instruction);
        }

        readonly UnityCoroutineRunner<T>.Process<UnityCoroutineRunner<T>.RunningTasksInfo> _processEnumerator;
    }
}
#endif