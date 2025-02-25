namespace Svelto.Tasks.Internal
{
    //Note: SerialFlow was once existing. However it was removed because it needed way to much code trickery to work
    //The best way to achieve serial execution is anyway to use SerialTaskCollection. 
    
    public interface IFlowModifier
    {
        bool CanMoveNext<T>(ref int nextIndex, int coroutinesCount, bool hasCoroutineCompleted) where T : ISveltoTask;
        bool CanProcessThis(ref int index);
        void Reset();
    }
}