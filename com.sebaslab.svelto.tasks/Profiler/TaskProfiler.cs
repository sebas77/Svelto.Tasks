#if TASKS_PROFILER_ENABLED
//#define ENABLE_PIX_EVENTS

using System;
using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using Svelto.DataStructures;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public static class TaskProfiler
    {
        static readonly ThreadLocal<Stopwatch> _stopwatch = new ThreadLocal<Stopwatch>(() => new Stopwatch());
        
        static readonly object LockObject = new object();

        static readonly FasterDictionary<RefWrapper<string>, FasterDictionary<RefWrapper<string>, TaskInfo>> taskInfos =
            new FasterDictionary<RefWrapper<string>, FasterDictionary<RefWrapper<string>, TaskInfo>>();

        public static StepState MonitorUpdateDuration<T>( ref T sveltoTask, string runnerName, (int index, int currentSpawnedTaskToRunIndex) valueTuple) where T : ISveltoTask
        {
            var taskName = sveltoTask.name;
#if ENABLE_PIX_EVENTS
            PixWrapper.PIXBeginEventEx(0x11000000, key);
#endif
            _stopwatch.Value.Start();
            var result = sveltoTask.Step(valueTuple.index, valueTuple.currentSpawnedTaskToRunIndex);
            _stopwatch.Value.Stop();
#if ENABLE_PIX_EVENTS
            PixWrapper.PIXEndEventEx();
#endif
            lock (LockObject)
            {
                ref var infosPerRunnner = ref taskInfos.GetOrAdd(runnerName, () => new FasterDictionary<RefWrapper<string>, TaskInfo>());
                string pattern = @"^.*\.(\w+)\+<(\w+)>d__\d+$";
                string replacement = "$1.$2";

// Perform the replacement
                taskName = Regex.Replace(taskName, pattern, replacement);
                
                if (infosPerRunnner.TryGetValue(taskName, out var info) == false)
                {
                    info = new TaskInfo(taskName, runnerName);
                    infosPerRunnner.Add(taskName, info);
                }
                else
                {
                    info.AddUpdateDuration((float) _stopwatch.Value.Elapsed.TotalMilliseconds);

                    infosPerRunnner[taskName] = info;
                }
            }

            _stopwatch.Value.Reset();

            return result;
        }

        public static void ResetDurations(string runnerName)
        {
            lock (LockObject)
            {
                if (taskInfos.TryGetValue(runnerName, out var info) == true)
                {
                    TaskInfo[] taskInfosValuesArray = info.GetValues(out var count).ToManagedArray();
                    for (var index = 0; index < count; index++)
                    {
                        taskInfosValuesArray[index].MarkNextFrame();
                    }
                }
            }
        }

        public static void ClearTasks()
        {
            lock (LockObject)
            {
                taskInfos.Clear();
            }
        }

        public static void CopyAndUpdate(ref TaskInfo[] infos)
        {
            lock (LockObject)
            {
                int totalCount = 0;

                foreach (KeyValuePairFast<RefWrapper<string>, FasterDictionary<RefWrapper<string>, TaskInfo>,
                    ManagedStrategy<FasterDictionary<RefWrapper<string>, TaskInfo>>> runner in taskInfos)
                    totalCount += runner.value.count;

                if (totalCount == 0)
                    return;

                if (infos == null || infos.Length != totalCount)
                    infos = new TaskInfo[totalCount];

                int currentCount = 0;

                foreach (KeyValuePairFast<RefWrapper<string>, FasterDictionary<RefWrapper<string>, TaskInfo>, ManagedStrategy<FasterDictionary<RefWrapper<string>, TaskInfo>>> runner in taskInfos)
                {
                    runner.value.CopyValuesTo(infos, (uint) currentCount);
                    currentCount += runner.value.count;
                }
            }
        }
    }
}
#endif