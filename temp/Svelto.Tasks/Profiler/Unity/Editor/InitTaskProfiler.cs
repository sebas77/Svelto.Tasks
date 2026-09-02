#if UNITY_EDITOR && TASKS_PROFILER_ENABLED
using UnityEngine;

namespace Svelto.Tasks.Profiler
{
    static class InitTaskProfiler
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitTaskProfilerMethod()
        {
            if (Application.isPlaying)
            {
                var debugTasksObject = GameObject.Find("Svelto.Tasks.Profiler");
                if (debugTasksObject == null)
                {
                    debugTasksObject = new GameObject("Svelto.Tasks.Profiler");
                    debugTasksObject.gameObject.AddComponent<TasksProfilerBehaviour>();
                    Object.DontDestroyOnLoad(debugTasksObject);
                }
            }
        }
    }
}
#endif