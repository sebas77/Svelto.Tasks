using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public class TasksProfilerBehaviour : MonoBehaviour
    {
        public Dictionary<IEnumerator, TaskInfo>.ValueCollection tasks { get { return TaskProfiler.taskInfos.Values; } }

        public void ResetDurations()
        {
            TaskProfiler.ResetDurations();
        }
    }
}
