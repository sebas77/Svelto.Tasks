#if UNITY_EDITOR && TASKS_PROFILER_ENABLED
using Svelto.DataStructures;
using UnityEngine;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public class TaskMonitor
    {
        [RuntimeInitializeOnLoadMethod]
        static void TaskMonitorSetup()
        {
            var debugTasksObject = UnityEngine.GameObject.Find("Svelto.Tasks.Profiler");
            if (debugTasksObject == null)
            {
                debugTasksObject = new UnityEngine.GameObject("Svelto.Tasks.Profiler");
                debugTasksObject.gameObject.AddComponent<Svelto.Tasks.Profiler.TasksProfilerBehaviour>();
                UnityEngine.Object.DontDestroyOnLoad(debugTasksObject);
            }
        }
    }
        
    
    public class TasksProfilerBehaviour : MonoBehaviour
    {
        public FasterList<TaskInfo>  tasks => TaskProfiler.taskInfos.Values;

        public void ResetDurations()
        {
            TaskProfiler.ResetDurations();
        }
    }
}
#endif
