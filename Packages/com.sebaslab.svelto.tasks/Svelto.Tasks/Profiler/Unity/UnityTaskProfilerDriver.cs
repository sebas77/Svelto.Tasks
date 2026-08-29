//Unity-only backend: the UNITY_* symbol keeps this file out of plain .NET builds,
//where the profiling define can still be used by custom ITaskProfilerDriver plugins
#if (UNITY_5 || UNITY_5_3_OR_NEWER) && TASKS_PROFILER_ENABLED
using System;
using System.Collections.Concurrent;
using Unity.Profiling;
using UnityEngine;

namespace Svelto.Tasks.Profiler
{
    /// <summary>
    /// Bridges task-profiler scopes to Unity Profiler samples. Installs itself as
    /// <see cref="TaskProfiler.Driver"/> at startup; assign a new instance manually to override.
    /// </summary>
    public sealed class UnityTaskProfilerDriver : ITaskProfilerDriver, ITaskProfilerThreadDriver
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InstallDriver()
        {
            TaskProfiler.Driver = new UnityTaskProfilerDriver();
            _createRunnerMarker = CreateRunnerMarker;
            _createTaskMarker = CreateTaskMarker;
        }

        public const string CategoryName = "Svelto.Tasks";
        public const string TaskTimeCounterName = "Task Time";
        public const string TaskStepsCounterName = "Task Steps";

        static readonly ProfilerCategory _category = new ProfilerCategory(CategoryName, ProfilerCategoryColor.Scripts);
        static readonly object _counterLock = new object();

        static readonly ProfilerCounterValue<long> _taskTime = new ProfilerCounterValue<long>(_category,
            TaskTimeCounterName, ProfilerMarkerDataUnit.TimeNanoseconds,
            ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        static readonly ProfilerCounterValue<int> _taskSteps = new ProfilerCounterValue<int>(_category,
            TaskStepsCounterName, ProfilerMarkerDataUnit.Count,
            ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        static Func<string, ProfilerMarker> _createRunnerMarker;
        static Func<string, ProfilerMarker> _createTaskMarker;

        readonly ConcurrentDictionary<string, ProfilerMarker> _runnerMarkers =
            new ConcurrentDictionary<string, ProfilerMarker>();

        readonly ConcurrentDictionary<string, ProfilerMarker> _taskMarkers =
            new ConcurrentDictionary<string, ProfilerMarker>();

        public void BeginRunner(string runnerName)
        {
            _runnerMarkers.GetOrAdd(runnerName, _createRunnerMarker).Begin();
        }

        public void EndRunner(string runnerName)
        {
            _runnerMarkers[runnerName].End();
        }

        public void BeginWorkerThread(string runnerName)
        {
            UnityEngine.Profiling.Profiler.BeginThreadProfiling(CategoryName, runnerName);
        }

        public void EndWorkerThread()
        {
            UnityEngine.Profiling.Profiler.EndThreadProfiling();
        }

        public void BeginTask(string runnerName, string taskName)
        {
            _taskMarkers.GetOrAdd(taskName, _createTaskMarker).Begin();
        }

        public void EndTask(string runnerName, string taskName, float elapsedMilliseconds)
        {
            _taskMarkers[taskName].End();

#if ENABLE_PROFILER
            lock (_counterLock)
            {
                _taskTime.Value += (long)(elapsedMilliseconds * 1_000_000.0f);
                _taskSteps.Value++;
            }
#endif
        }

        static ProfilerMarker CreateRunnerMarker(string runnerName)
        {
            return new ProfilerMarker(_category, $"Runner/{runnerName}");
        }

        static ProfilerMarker CreateTaskMarker(string taskName)
        {
            return new ProfilerMarker(_category, TaskProfiler.NormalizeTaskName(taskName));
        }
    }
}
#endif
