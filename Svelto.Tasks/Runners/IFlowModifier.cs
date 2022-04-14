namespace Svelto.Tasks.Internal
{
    public interface IFlowModifier
    {
        bool CanMoveNext<T>(ref int nextIndex, ref T currentResult, int coroutinesCount, bool result) where T : ISveltoTask;
        bool CanProcessThis(ref int index);
        void Reset();
        
        string runnerName { get; set; }
    }
}