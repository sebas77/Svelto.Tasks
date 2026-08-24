#if TASKS_PROFILER_ENABLED
using System;
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

        static readonly Regex _iteratorNameRegex = new Regex(@"^.*\.(\w+)\s+<(\w+)>d__\d+$");

        static readonly FasterDictionary<RefWrapper<string>, FasterDictionary<RefWrapper<string>, TaskInfo>> taskInfos =
            new FasterDictionary<RefWrapper<string>, FasterDictionary<RefWrapper<string>, TaskInfo>>();

        /// <summary>
        /// Optional plugin used to instrument each task step with an external profiling API (e.g. PIX).
        /// When null, only the built-in stopwatch instrumentation runs.
        /// </summary>
        public static ITaskProfilerPlugin Plugin { get; set; }

        public static StepState MonitorUpdateDuration<T>(ref T sveltoTask, string runnerName,
            (int index, TombstoneHandle currentSpawnedTaskToRunIndex) valueTuple) where T : ISveltoTask
        {
            var taskName = sveltoTask.name;

            _stopwatch.Value.Start();

            StepState result;
            try
            {
                Plugin?.BeginStep(taskName);
                result = sveltoTask.Step(valueTuple.index, valueTuple.currentSpawnedTaskToRunIndex);
            }
            finally
            {
                Plugin?.EndStep();
                _stopwatch.Value.Stop();
            }

            lock (LockObject)
            {
                ref var infosPerRunnner = ref taskInfos.GetOrAdd(runnerName,
                    () => new FasterDictionary<RefWrapper<string>, TaskInfo>());

                //GetOrAdd only invokes the builder on the first insert, so the regex name is paid once per task
                ref var info = ref infosPerRunnner.GetOrAdd(taskName,
                    () => new TaskInfo(_iteratorNameRegex.Replace(taskName, "$1.$2"), runnerName));

                info.AddUpdateDuration((float)_stopwatch.Value.Elapsed.TotalMilliseconds);
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
                {
                    totalCount += runner.value.count;
                }

                if (totalCount == 0)
                {
                    infos = Array.Empty<TaskInfo>();
                    return;
                }

                if (infos == null || infos.Length != totalCount)
                    infos = new TaskInfo[totalCount];

                int currentCount = 0;

                foreach (var (key, value) in taskInfos)
                {
                    value.CopyValuesTo(infos, (uint)currentCount);
                    currentCount += value.count;
                }
            }
        }
    }
}
#endif
