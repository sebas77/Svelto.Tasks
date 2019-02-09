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
            try
            {
                if (Application.isPlaying)
                {
                    GameObject go = new GameObject("Svelto.Tasks.UnityScheduler");
                    _runnerBehaviour = go.AddComponent<RunnerBehaviourUpdate>();

                    Object.DontDestroyOnLoad(go);
                }
            }
            catch
            {
                Svelto.Console.LogError
                    ("While Unity runners can be referenced from inside other threads, " +
                     "their very first use must happen inside the Unity main thread");
            }
        }
        
        public static void StartUpdateCoroutine(IProcessSveltoTasks process)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartUpdateCoroutine(process);
        }
        
        public static void StartEarlyUpdateCoroutine(IProcessSveltoTasks process)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartEarlyUpdateCoroutine(process);
        }
        
        public static void StartEndOfFrameCoroutine(IProcessSveltoTasks process)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartEndOfFrameCoroutine(process);
        }
        
        public static void StartLateCoroutine(IProcessSveltoTasks process)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartLateCoroutine(process);
        }

        public static void StartCoroutine(IProcessSveltoTasks process)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartSveltoCoroutine(process);
        }
        
        public static void StartYieldCoroutine(IEnumerator yieldInstructionWrapper)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartCoroutine(yieldInstructionWrapper);
        }

        public static void StartPhysicCoroutine(IProcessSveltoTasks process)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartPhysicCoroutine(process);
        }
    }
}
#endif
