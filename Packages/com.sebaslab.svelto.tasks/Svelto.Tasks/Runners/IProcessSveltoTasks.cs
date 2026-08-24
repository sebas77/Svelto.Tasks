using Svelto.Common;
using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    public interface IProcessSveltoTasks<TTask> where TTask : ISveltoTask
    {
        bool MoveNext<PlatformProfiler>(in PlatformProfiler platformProfiler)
            where PlatformProfiler : IPlatformProfiler;

        uint numberOfRunningTasks { get; }
        uint numberOfQueuedTasks { get; }
        uint numberOfTasks { get; }
        
        void AddTask(in TTask task, (int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex) index);
    }
}