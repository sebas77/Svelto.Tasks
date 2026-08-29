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

        static readonly Regex _iteratorNameRegex = new Regex(@"^.*\.(\w+)\+<(\w+)>d__\d+$");

        //nested generic wrapper enumerators report the wrapped task type, e.g.
        //Ns.Outer`1+WrapEnumerator[[Ns.Inner.Type, Assembly-CSharp]] renders as Ns.Inner.Type
        static readonly Regex _wrapperNameRegex =
            new Regex(@"^.*\.(\w+)`\d+\+(\w+)\[\[(\w+(\.\w+)*), .*$");

        //unknown task shapes still must not leak assembly qualifiers
        static readonly Regex _assemblyQualifierRegex =
            new Regex(@",\s*\w[\w.-]*,\s*Version=[^,\]]*,\s*Culture=[^,\]]*,\s*PublicKeyToken=[^\],]*");

        static readonly FasterDictionary<RefWrapper<string>, FasterDictionary<RefWrapper<string>, TaskInfo>> taskInfos =
            new FasterDictionary<RefWrapper<string>, FasterDictionary<RefWrapper<string>, TaskInfo>>();

        static ITaskProfilerDriver _driver;

        /// <summary>
        /// Optional backend used to instrument runner and task scopes with an external profiling API.
        /// When null, only the built-in task timing data is collected.
        /// </summary>
        public static ITaskProfilerDriver Driver
        {
            get => Volatile.Read(ref _driver);
            set => Volatile.Write(ref _driver, value);
        }

        internal static ITaskProfilerDriver BeginRunner(string runnerName)
        {
            var driver = Driver;
            driver?.BeginRunner(runnerName);

            return driver;
        }

        internal static void EndRunner(ITaskProfilerDriver driver, string runnerName)
        {
            driver?.EndRunner(runnerName);
        }

        internal static ITaskProfilerThreadDriver BeginWorkerThread(string runnerName)
        {
            var driver = Driver as ITaskProfilerThreadDriver;
            driver?.BeginWorkerThread(runnerName);

            return driver;
        }

        internal static void EndWorkerThread(ITaskProfilerThreadDriver driver)
        {
            driver?.EndWorkerThread();
        }

        public static StepState MonitorUpdateDuration<T>(ref T sveltoTask, string runnerName,
            (int index, TombstoneHandle currentSpawnedTaskToRunIndex) valueTuple) where T : ISveltoTask
        {
            var taskName = sveltoTask.name;

            StepState result;
            float elapsedMilliseconds = 0;
            var driver = Driver;

            driver?.BeginTask(runnerName, taskName);
            try
            {
                var stopwatch = _stopwatch.Value;
                stopwatch.Restart();
                try
                {
                    result = sveltoTask.Step(valueTuple.index, valueTuple.currentSpawnedTaskToRunIndex);
                }
                finally
                {
                    stopwatch.Stop();
                    elapsedMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;
                    stopwatch.Reset();
                }
            }
            finally
            {
                driver?.EndTask(runnerName, taskName, elapsedMilliseconds);
            }

            lock (LockObject)
            {
                ref var infosPerRunnner = ref taskInfos.GetOrAdd(runnerName,
                    () => new FasterDictionary<RefWrapper<string>, TaskInfo>());

                //GetOrAdd(key) never allocates: a capturing builder delegate would allocate
                //a closure on every call, per task step, on every runner thread. The name is
                //normalized only on the first insert (taskName is null for a default TaskInfo)
                ref var info = ref infosPerRunnner.GetOrAdd(taskName);
                if (info.taskName == null)
                    info = new TaskInfo(NormalizeTaskName(taskName), runnerName);

                info.AddUpdateDuration(elapsedMilliseconds);
            }

            return result;
        }

        internal static string NormalizeTaskName(string taskName)
        {
            //compiler-generated iterator state machines: Ns.Outer+<Method>d__N -> Outer.Method
            taskName = _iteratorNameRegex.Replace(taskName, "$1.$2");

            //nested generic wrapper enumerators report the wrapped task type, e.g.
            //Ns.Outer`1+WrapEnumerator[[Ns.Inner.Type, Assembly-CSharp]] renders as Ns.Inner.Type
            if (_wrapperNameRegex.IsMatch(taskName) == true)
                return _wrapperNameRegex.Match(taskName).Groups[3].Value;

            //task implementations not covered by the patterns above keep their type name,
            //but never leak assembly qualifiers
            return _assemblyQualifierRegex.Replace(taskName, string.Empty);
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
