using System.Collections;

namespace Svelto.Tasks.Internal
{
    public interface IRunningTasksInfo
    {
        bool   CanMoveNext(ref int nextIndex, TaskContract currentResult);
        bool   CanProcessThis(ref int index);
        void   Reset();
        string runnerName { get; }
    }
    
    public interface IProcessSveltoTasks
    {
        bool MoveNext(bool immediate = false);
    }
}