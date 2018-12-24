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
        
        static object LOCK_OBJECT = new object();

        internal static readonly ThreadSafeDictionary<string, TaskInfo> taskInfos =
            new ThreadSafeDictionary<string, TaskInfo>();
 
        public static bool MonitorUpdateDuration<T>(ISveltoTask sveltoTask, string runnerName) where T : IEnumerator
        {
            var key = sveltoTask.ToString().FastConcat(runnerName);
#if ENABLE_PIX_EVENTS            
            PixWrapper.PIXBeginEventEx(0x11000000, key);
#endif    
            _stopwatch.Start();
    mettere platform profiler qui 
            var result = sveltoTask.MoveNext();
            _stopwatch.Stop();
#if ENABLE_PIX_EVENTS            
            PixWrapper.PIXEndEventEx();
#endif      
            lock (LOCK_OBJECT)
            {
                TaskInfo info;
                
                if (taskInfos.TryGetValue(key, out info) == false)
                {
                    info = new TaskInfo(sveltoTask.ToString());
                    info.AddThreadInfo(runnerName.FastConcat(": "));
                    taskInfos.Add(key, ref info);
                }
                else
                {
                    info.AddUpdateDuration(_stopwatch.Elapsed.TotalMilliseconds);
                    
                    taskInfos.Update(key, ref info);
                }
            }

            _stopwatch.Reset();

            return result;
        }

        public static void ResetDurations()
        {
            taskInfos.Clear();
        }
    }
}
#endif