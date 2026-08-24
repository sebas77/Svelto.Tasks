#if TASKS_PROFILER_ENABLED

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    /// <summary>
    /// Lets the task profiler instrument each task step with any external profiling API (e.g. PIX, Unity Profiler...).
    /// Assign an implementation to TaskProfiler.Plugin. The built-in stopwatch instrumentation always runs;
    /// the plugin is additive. BeginStep/EndStep are guaranteed to be balanced (EndStep runs even if the task faults).
    /// </summary>
    public interface ITaskProfilerPlugin
    {
        void BeginStep(string taskName);
        void EndStep();
    }
}
#endif
