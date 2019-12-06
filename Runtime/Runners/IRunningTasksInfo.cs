using Svelto.Common;

namespace Svelto.Tasks.Internal
{
    public interface IRunningTasksInfo
    {
        bool CanMoveNext<T>(ref int nextIndex, ref T currentResult, int coroutinesCount);
        bool CanProcessThis(ref int index);
        void Reset();
        
        string runnerName { get; set; }
    }
    
    public interface IProcessSveltoTasks
    {
        bool MoveNext<PlatformProfiler>(bool immediate, in PlatformProfiler platformProfiler)
            where PlatformProfiler : IPlatformProfiler<DisposableSampler>;
    }
}