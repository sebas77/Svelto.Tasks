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

        static readonly FasterDictionary<string, FasterDictionary<string, TaskInfo>> taskInfos =
            new FasterDictionary<string, FasterDictionary<string, TaskInfo>>();

        public static bool MonitorUpdateDuration<T>(ISveltoTask<T> sveltoTask, string runnerName) where T : IEnumerator
        {
            var taskName = sveltoTask.ToString();
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
                ref var infosPerRunnner =
                    ref taskInfos.GetOrCreate(runnerName, () => new FasterDictionary<string, TaskInfo>());
                if (infosPerRunnner.TryGetValue(taskName, out var info) == false)
                {
                    info = new TaskInfo(taskName, runnerName);
                    infosPerRunnner.Add(taskName, info);
                }
                else
                {
                    info.AddUpdateDuration((float) _stopwatch.Elapsed.TotalMilliseconds);

                    infosPerRunnner[taskName] = info;
                }
            }

            _stopwatch.Reset();

            return result;
        }

        public static void ResetDurations(string runnerName)
        {
            if (taskInfos.TryGetValue(runnerName, out var info) == true)
            {
                TaskInfo[] taskInfosValuesArray = info.GetValuesArray(out var count);
                for (var index = 0; index < count; index++)
                {
                    taskInfosValuesArray[index].MarkNextFrame();
                }
            }
        }

        public static void ClearTasks()
        {
            taskInfos.FastClear();
        }

        public static void CopyAndUpdate(ref TaskInfo[] infos)
        {
            lock (_stopwatch)
            {
                int count = 0;

                foreach (var runner in taskInfos) count += runner.Value.Count;

                if (infos == null || infos.Length != count)
                    infos = new TaskInfo[count];

                count = 0;

                foreach (var runner in taskInfos)
                {
                    runner.Value.CopyValuesTo(infos, (uint) count);
                    count += runner.Value.Count;
                }
            }
        }
    }
}
#endif