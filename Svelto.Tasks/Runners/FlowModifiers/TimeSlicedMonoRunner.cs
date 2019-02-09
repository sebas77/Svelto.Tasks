using System.Diagnostics;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.Unity
{
    public struct TimeSlicedRunningInfo : IRunningTasksInfo
        {
            public TimeSlicedRunningInfo(float maxMilliseconds)
            {
                maxTicks = (long) (maxMilliseconds * 10000);
                _stopWatch = new Stopwatch();
                runnerName = null;
            }

            public bool CanMoveNext(ref int nextIndex, TaskContract currentResult, int coroutineCount)
            {
                //never stops until maxMilliseconds is elapsed or Break.AndResumeNextIteration is returned
                if (_stopWatch.ElapsedTicks > maxTicks)
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
            readonly long maxTicks;
        }
}
