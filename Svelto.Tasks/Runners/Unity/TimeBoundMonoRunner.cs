#if later
#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using System.Diagnostics;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    //TimeBoundMonoRunner ensures that the tasks running won't take more than maxMilliseconds per iteration
    //Several tasks must run on this runner to make sense. TaskCollections are considered
    //single tasks, so they don't count (may change in future)
    /// </summary>
    public class TimeBoundMonoRunner : TimeBoundMonoRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
    {
        public TimeBoundMonoRunner(string name, float maxMilliseconds) : base(name, maxMilliseconds)
        {
        }
    }
    public class TimeBoundMonoRunner<T> : BaseRunner<T> where T: ISveltoTask
    {
        public float maxMilliseconds
        {
            set
            {
                _info.maxMilliseconds = (long) (value * 10000);
            }
        }

        // Greedy means that the runner will try to occupy the whole maxMilliseconds interval, by looping among all tasks until all are completed or maxMilliseconds passed
        public TimeBoundMonoRunner(string name, float maxMilliseconds):base(name)
        {
            _flushingOperation = new UnityCoroutineRunner<T>.FlushingOperation();

            _info = new TimeBoundRunningInfo(maxMilliseconds)
            {
                runnerName = name
            };

            StartProcess(new UnityCoroutineRunner<T>.Process<TimeBoundRunningInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, _info));
        }
        
        struct TimeBoundRunningInfo : IRunningTasksInfo
        {
            public long maxMilliseconds;

            public TimeBoundRunningInfo(float maxMilliseconds):this()
            {
                this.maxMilliseconds = (long) (maxMilliseconds * 10000);
                _stopWatch = new Stopwatch();
            }
            
            public bool CanMoveNext(ref int nextIndex, TaskContract currentResult)
            {
                if (_stopWatch.ElapsedTicks > maxMilliseconds)
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

        }

        TimeBoundRunningInfo _info;
    }
}
#endif
#endif