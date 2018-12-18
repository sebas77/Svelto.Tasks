using System.Collections;

namespace Svelto.Tasks.Unity.Internal
{
    public interface IRunningTasksInfo<T> where T:IEnumerator
    {
        bool CanMoveNext(ref int nextIndex, TaskContract? currentResult);
        bool   CanProcessThis(ref int index);
        void   Reset();
        string runnerName { get; }
    }
}