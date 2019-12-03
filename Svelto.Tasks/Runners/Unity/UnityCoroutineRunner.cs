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

        public static void StartUpdateCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartUpdateCoroutine(process, runningOrder);
        }

        public static void StartEarlyUpdateCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartEarlyUpdateCoroutine(process, runningOrder);
        }

        public static void StartEndOfFrameCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartEndOfFrameCoroutine(process, runningOrder);
        }

        public static void StartLateCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartLateCoroutine(process, runningOrder);
        }

        public static void StartCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartSveltoCoroutine(process, runningOrder);
        }

        public static void StartYieldInstructionCoroutine(IEnumerator yieldInstructionWrapper)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartCoroutine(yieldInstructionWrapper);
        }

        public static void StartPhysicCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            if (_runnerBehaviour != null)
                _runnerBehaviour.StartPhysicCoroutine(process, runningOrder);
        }
    }
}
#endif
