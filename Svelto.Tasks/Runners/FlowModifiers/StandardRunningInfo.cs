using Svelto.Tasks.Internal;

namespace Svelto.Tasks.FlowModifiers
{
    public struct StandardRunningInfo:IFlowModifier
    {
        public bool CanMoveNext<T>(ref int nextIndex, ref T currentResult, int coroutinesCount)
        {
            return true;
        }

        public bool CanProcessThis(ref int index)
        {
            return true;
        }

        public void Reset()
        {}

        public string runnerName { get; set; }
    }
}