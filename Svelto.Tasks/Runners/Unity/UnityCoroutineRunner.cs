#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.Tasks.Internal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Svelto.Tasks.Unity.Internal
{
    public static class UnityCoroutineRunner
    {
        static RunnerBehaviourUpdate _runnerBehaviour;

        static RunnerBehaviourUpdate runnerBehaviour
        {
            get
            {
                if (_runnerBehaviour == null)
                {
                    GameObject go = GameObject.Find("Svelto.Tasks.UnityScheduler");
                    if (go != null)
                        GameObject.DestroyImmediate(go);
                    go = new GameObject("Svelto.Tasks.UnityScheduler");
                    _runnerBehaviour = go.AddComponent<RunnerBehaviourUpdate>();

                    Object.DontDestroyOnLoad(go);
                }

                return _runnerBehaviour;
            }
        }

        public static void StartUpdateCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            runnerBehaviour.StartUpdateCoroutine(process, runningOrder);
        }

        public static void StartEarlyUpdateCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            runnerBehaviour.StartEarlyUpdateCoroutine(process, runningOrder);
        }

        public static void StartEndOfFrameCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            runnerBehaviour.StartEndOfFrameCoroutine(process, runningOrder);
        }

        public static void StartLateCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            runnerBehaviour.StartLateCoroutine(process, runningOrder);
        }

        public static void StartCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            runnerBehaviour.StartSveltoCoroutine(process, runningOrder);
        }

        public static void StartYieldInstructionCoroutine(IEnumerator yieldInstructionWrapper)
        {
            runnerBehaviour.StartCoroutine(yieldInstructionWrapper);
        }

        public static void StartPhysicCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            runnerBehaviour.StartPhysicCoroutine(process, runningOrder);
        }

        public static void StartOnGuiCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            runnerBehaviour.StartOnGuiCoroutine(process, runningOrder);
        }
    }
}
#endif
