#if TASKS_PROFILER_ENABLED

namespace Svelto.Tasks.Profiler
{
    /// <summary>
    /// Receives balanced runner and task scopes from <see cref="TaskProfiler"/>.
    /// Implementations may bridge Svelto.Tasks to any profiling backend without introducing
    /// platform-specific dependencies into the task scheduler.
    /// </summary>
    public interface ITaskProfilerDriver
    {
        void BeginRunner(string runnerName);
        void EndRunner(string runnerName);

        void BeginTask(string runnerName, string taskName);
        void EndTask(string runnerName, string taskName, float elapsedMilliseconds);
    }

    //Optional Unity-facing lifecycle hook. Keeping this separate avoids changing the public profiler-driver contract.
    internal interface ITaskProfilerThreadDriver
    {
        void BeginWorkerThread(string runnerName);
        void EndWorkerThread();
    }
}
#endif
