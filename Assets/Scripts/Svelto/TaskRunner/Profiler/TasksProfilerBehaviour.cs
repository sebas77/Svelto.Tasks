#if UNITY_EDITOR
using Svelto.DataStructures;
using UnityEngine;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public class TasksProfilerBehaviour : MonoBehaviour
    {
        public FasterList<TaskInfo>  tasks { get { return TaskProfiler.taskInfos.Values; } }

        public void ResetDurations()
        {
            TaskProfiler.ResetDurations();
        }
    }
}
#endif