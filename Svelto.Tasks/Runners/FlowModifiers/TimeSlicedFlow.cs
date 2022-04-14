using System.Diagnostics;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.FlowModifiers
{
    public struct TimeSlicedFlow : IFlowModifier
    {
        public TimeSlicedFlow(float maxMilliseconds)
        {
            _maxTicks = (long) (maxMilliseconds * 10000);
            _stopWatch = new Stopwatch();
            runnerName = null;
        }

        public bool CanMoveNext<T>(ref int nextIndex, ref T currentResult, int coroutineCount, bool result) where T:ISveltoTask
        {
            //never stops until maxMilliseconds is elapsed or Break.AndResumeNextIteration is returned
            if (_stopWatch.ElapsedTicks > _maxTicks)
            {
                _stopWatch.Reset();
                _stopWatch.Start();

                return false;
            }

            if (nextIndex >= coroutineCount)
                nextIndex = 0; //restart iteration and continue

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
        readonly long      _maxTicks;
    }
}