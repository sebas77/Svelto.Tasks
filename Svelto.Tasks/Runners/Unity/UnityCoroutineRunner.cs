#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.Tasks.Internal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Svelto.Tasks.Unity.Internal
{
    public static class UnityCoroutineRunner
    {
        static readonly RunnerBehaviourUpdate _runnerBehaviour;

        static UnityCoroutineRunner()
        {
            if (Application.isPlaying)
            {
                GameObject go = new GameObject("Svelto.Tasks.UnityScheduler");
                _runnerBehaviour = go.AddComponent<RunnerBehaviourUpdate>();

                Object.DontDestroyOnLoad(go);
            }
        }
        
        public static void StartUpdateCoroutine(IProcessSveltoTasks process)
        {
            if (Application.isPlaying)
                _runnerBehaviour.StartUpdateCoroutine(process);
        }
        
        public static void StartEarlyUpdateCoroutine(IProcessSveltoTasks process)
        {
            if (Application.isPlaying)
                _runnerBehaviour.StartEarlyUpdateCoroutine(process);
        }
        
        public static void StartEndOfFrameCoroutine(IProcessSveltoTasks process)
        {
            if (Application.isPlaying)
                _runnerBehaviour.StartEndOfFrameCoroutine(process);
        }
        
        public static void StartLateCoroutine(IProcessSveltoTasks process)
        {
            if (Application.isPlaying)
                _runnerBehaviour.StartLateCoroutine(process);
        }

        public static void StartCoroutine(IProcessSveltoTasks process)
        {
            if (Application.isPlaying)
                _runnerBehaviour.StartSveltoCoroutine(process);
        }
        
        public static void StartYieldCoroutine(IEnumerator yieldInstructionWrapper)
        {
            if (Application.isPlaying)
                _runnerBehaviour.StartCoroutine(yieldInstructionWrapper);
        }

        public static void StartPhysicCoroutine(IProcessSveltoTasks process)
        {
            if (Application.isPlaying)
                _runnerBehaviour.StartPhysicCoroutine(process);
        }
    }
}
#endif
