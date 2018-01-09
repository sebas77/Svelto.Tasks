using System.Collections;
using System.Diagnostics;
using Svelto.DataStructures;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public sealed class TaskProfiler
    {
        static readonly Stopwatch _stopwatch = new Stopwatch();

        internal static readonly ThreadSafeDictionary<string, TaskInfo> taskInfos =
            new ThreadSafeDictionary<string, TaskInfo>();

        public static bool MonitorUpdateDuration(IEnumerator tickable, string runnerName)
        {
            bool value = MonitorUpdateDurationInternal(tickable, runnerName.FastConcat(": "));

            return value;
        }

        public static void ResetDurations()
        {
            taskInfos.Clear();
        }

        static bool MonitorUpdateDurationInternal(IEnumerator tickable, string threadInfo)
        {
            TaskInfo info;

            bool result;
            
            _stopwatch.Start();
            result = tickable.MoveNext();
            _stopwatch.Stop();

            if (taskInfos.TryGetValue(tickable.ToString(), out info) == false)
            {
                info = new TaskInfo(tickable);

                info.AddUpdateDuration(_stopwatch.Elapsed.TotalMilliseconds);

                info.AddThreadInfo(threadInfo);

                taskInfos.Add(tickable.ToString(), info);
            }
            else
            {
                info.AddUpdateDuration(_stopwatch.Elapsed.TotalMilliseconds);

                info.AddThreadInfo(threadInfo);
            }
            
            _stopwatch.Reset();

            return result;
        }
    }
}