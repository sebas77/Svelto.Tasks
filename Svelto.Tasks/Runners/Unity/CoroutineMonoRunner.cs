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
    public class CoroutineMonoRunner : MonoRunner
    {
        public CoroutineMonoRunner(string name, bool mustSurvive = false):base(name)
        {
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            _runnerBehaviour = _go.AddComponent<RunnerBehaviour>();
            
            var info = new UnityCoroutineRunner.RunningTasksInfo { runnerName = name };

            _processEnumerator = new UnityCoroutineRunner.Process<UnityCoroutineRunner.RunningTasksInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, info);
            
            _runnerBehaviour.StartCoroutine(_processEnumerator);

        }
        
        public override void StartCoroutine(IPausableTask task)
        {
            paused = false;

            _newTaskRoutines.Enqueue(task);
            _flushingOperation.immediate = true;
            _processEnumerator.MoveNext();
            _flushingOperation.immediate = false;
        }

        readonly UnityCoroutineRunner.Process<UnityCoroutineRunner.RunningTasksInfo> _processEnumerator;
        readonly MonoBehaviour _runnerBehaviour;

        public void StartYieldInstruction(IEnumerator instruction)
        {
            _runnerBehaviour.StartCoroutine(instruction);
        }
    }
}
#endif