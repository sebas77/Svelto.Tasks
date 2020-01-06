#if UNITY_EDITOR && TASKS_PROFILER_ENABLED
using Svelto.DataStructures;
using UnityEngine;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public class TasksProfilerBehaviour : MonoBehaviour
    {
        public ThreadSafeDictionary<string, TaskInfo> tasks => TaskProfiler.taskInfos;

        public void ResetDurations()
        {
            TaskProfiler.ResetDurations();
        }
    }
}
#endif
