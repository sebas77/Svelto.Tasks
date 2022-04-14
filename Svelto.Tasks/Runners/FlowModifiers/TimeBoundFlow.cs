using System.Diagnostics;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.FlowModifiers
{
    /// <summary>
    ///TimeBoundRunningInfo ensures that the tasks running won't take more than maxMilliseconds per iteration.
    ///Several tasks must run on the runner to make sense. TaskCollections are considered
    ///single tasks, so they don't count (may change in future)
    /// </summary>
    public struct TimeBoundFlow : IFlowModifier
    {
        public TimeBoundFlow(float maxMilliseconds) : this()
        {
            _maxMilliseconds = (long) (maxMilliseconds * 10000);
            _stopWatch       = new Stopwatch();
        }

        public bool CanMoveNext<T>(ref int nextIndex, ref T currentResult, int coroutinesCount, bool result) where T:ISveltoTask
        {
            if (_stopWatch.ElapsedTicks > _maxMilliseconds)
                return false;

            return true;
        }

        public bool CanProcessThis(ref int index)
        {
            return true;
        }

        public void Reset()
        {
            _stopWatch.Reset();
            _stopWatch.Start();
        }

        public string runnerName { get; set; }

        readonly Stopwatch _stopWatch;
        readonly long      _maxMilliseconds;
    }
}