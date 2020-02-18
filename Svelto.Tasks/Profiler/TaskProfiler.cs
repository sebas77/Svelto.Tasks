#if TASKS_PROFILER_ENABLED
 //#define ENABLE_PIX_EVENTS

using System.Collections;
using System.Diagnostics;
using Svelto.DataStructures;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public static class TaskProfiler
    {
        static readonly Stopwatch _stopwatch = new Stopwatch();
        static readonly FasterDictionary<string, TaskInfo> taskInfos = new FasterDictionary<string, TaskInfo>();
 
        public static bool MonitorUpdateDuration<T>(ISveltoTask<T> sveltoTask, string runnerName) where T : IEnumerator
        {
            var key = sveltoTask.ToString().FastConcat(runnerName);
#if ENABLE_PIX_EVENTS            
            PixWrapper.PIXBeginEventEx(0x11000000, key);
#endif    
            _stopwatch.Start();
            var result = sveltoTask.MoveNext();
            _stopwatch.Stop();
#if ENABLE_PIX_EVENTS            
            PixWrapper.PIXEndEventEx();
#endif      
            lock (_stopwatch)
            {
                if (taskInfos.TryGetValue(key, out var info) == false)
                {
                    info = new TaskInfo(sveltoTask.ToString(), runnerName.FastConcat(": "));
                    taskInfos.Add(key, info);
                }
                else
                {
                    info.AddUpdateDuration((float) _stopwatch.Elapsed.TotalMilliseconds);
                    
                    taskInfos[key] = info;
                }
            }

            _stopwatch.Reset();

            return result;
        }

        public static void ResetDurations()
        {
            TaskInfo[] taskInfosValuesArray = taskInfos.GetValuesArray(out var count);
            for (var index = 0; index < count; index++)
            {
                taskInfosValuesArray[index].MarkNextFrame();
            }
        }
        
        public static void ClearTasks()
        {
            taskInfos.Clear();
        }

        public static void CopyAndUpdate(ref TaskInfo[] infos)
        {
            lock (_stopwatch)
            {
                if (infos == null || infos.Length != taskInfos.Count) 
                    infos = new TaskInfo[taskInfos.Count];
                
                taskInfos.CopyValuesTo(infos);
            }
        }
    }
}
#endif