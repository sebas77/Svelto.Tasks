#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.Tasks.Internal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Svelto.Tasks.Unity.Internal
{
    class YieldInstructionRunnerBehaviourUpdate : MonoBehaviour
    {
    }

    static class UnityCoroutineRunner
    {
        static YieldInstructionRunnerBehaviourUpdate _runnerBehaviour;

        static YieldInstructionRunnerBehaviourUpdate runnerBehaviour
        {
            get
            {
                if (_runnerBehaviour == null)
                {
                    GameObject go = GameObject.Find("Svelto.Tasks.UnityScheduler");
                    if (go != null)
                        Object.DestroyImmediate(go);
                    go = new GameObject("Svelto.Tasks.UnityScheduler");
                    _runnerBehaviour = go.AddComponent<YieldInstructionRunnerBehaviourUpdate>();

                    Object.DontDestroyOnLoad(go);
                }

                return _runnerBehaviour;
            }
        }

        public static void StartYieldInstructionCoroutine(IEnumerator yieldInstructionWrapper)
        {
            runnerBehaviour.StartCoroutine(yieldInstructionWrapper);
        }
    }
}
#endif
