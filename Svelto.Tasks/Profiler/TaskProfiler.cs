using System.Collections;
using System.Diagnostics;
using Svelto.DataStructures;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public sealed class TaskProfiler
    {
        readonly Stopwatch _stopwatch = new Stopwatch();
        
        static object LOCK_OBJECT = new object();

        internal static readonly ThreadSafeDictionary<string, TaskInfo> taskInfos =
            new ThreadSafeDictionary<string, TaskInfo>();

        public bool MonitorUpdateDuration(IEnumerator tickable, string runnerName)
        {
            _stopwatch.Start();
            var result = tickable.MoveNext();
            _stopwatch.Stop();
            var key = tickable.ToString().FastConcat(runnerName);
            
            lock (LOCK_OBJECT)
            {
                TaskInfo info;
                
                if (taskInfos.TryGetValue(key, out info) == false)
                {
                    info = new TaskInfo(tickable);
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