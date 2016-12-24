using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public sealed class TaskProfiler
    {
        static readonly Stopwatch _stopwatch = new Stopwatch();

        public static bool MonitorUpdateDuration(IEnumerator tickable)
        {
            TaskInfo info;

            bool result;

            if (taskInfos.TryGetValue(tickable, out info) == false)
            {
                info = new TaskInfo(tickable);

                taskInfos.Add(tickable, info);
            }

            _stopwatch.Reset();
            _stopwatch.Start();
            result = tickable.MoveNext();
            _stopwatch.Stop();

            info.AddUpdateDuration(_stopwatch.Elapsed.TotalMilliseconds);

            return result;
        }

        public static void ResetDurations()
        {
            taskInfos.Clear();
        }

        internal static readonly Dictionary<IEnumerator, TaskInfo> taskInfos = new Dictionary<IEnumerator, TaskInfo>();
    }
}
