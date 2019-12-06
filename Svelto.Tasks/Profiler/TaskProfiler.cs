#if TASKS_PROFILER_ENABLED
//#define ENABLE_PIX_EVENTS

using System.Diagnostics;
using Svelto.Common;
using Svelto.Common.Internal;
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

        public static bool MonitorUpdateDuration<T, PP>(ref T sveltoTask, string runnerName, PP profiler)
            where T : ISveltoTask where PP : IPlatformProfiler
        {
            var samplerName = sveltoTask.name;
            var key = samplerName.FastConcat(runnerName);
            bool result;
            using (profiler.Sample(samplerName))
            {
                _stopwatch.Start();
#if ENABLE_PIX_EVENTS
            PixWrapper.PIXBeginEventEx(0x11000000, key);
#endif
                result = sveltoTask.MoveNext();
#if ENABLE_PIX_EVENTS
            PixWrapper.PIXEndEventEx();
#endif
                _stopwatch.Stop();
            }

            lock (LOCK_OBJECT)
            {
                if (taskInfos.TryGetValue(key, out var info) == false)
                {
                    info = new TaskInfo(samplerName);
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

        public static void ResetDurations() { taskInfos.Clear(); }
    }
}
#endif