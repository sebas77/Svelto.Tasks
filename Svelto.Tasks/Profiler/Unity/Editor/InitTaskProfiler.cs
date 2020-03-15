#if UNITY_EDITOR && TASKS_PROFILER_ENABLED
using UnityEngine;

namespace Svelto.Tasks.Profiler
{
    static class InitTaskProfiler
    {
#if UNITY_2018_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
#endif
        static void InitTaskProfilerMethod()
        {
            if (Application.isPlaying)
            {
                var debugTasksObject = GameObject.Find("Svelto.Tasks.Profiler");
                if (debugTasksObject == null)
                {
                    debugTasksObject = new GameObject("Svelto.Tasks.Profiler");
                    debugTasksObject.gameObject.AddComponent<TasksProfilerBehaviour>();
                    if (Application.isPlaying == true)
                        Object.DontDestroyOnLoad(debugTasksObject);
                }
            }
        }
    }
}
#endif