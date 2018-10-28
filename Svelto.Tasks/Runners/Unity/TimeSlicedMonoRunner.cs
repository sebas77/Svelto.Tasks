#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Diagnostics;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    //TimeSlicedMonoRunner ensures that the tasks run up to the maxMilliseconds time. If a task takes less than it, the
    //next ones will be executed until maxMilliseconds is reached.
    //TimeSlicedMonoRunner can work on single tasks, this means that it would force the task to run up to maxMilliseconds
    //per frame, unless Break.AndResumeIteration is returned.
    /// </summary>
    public class TimeSlicedMonoRunner : MonoRunner
    {
        public float maxMilliseconds
        {
            set
            {
                _info.maxMilliseconds = (long) (value * 10000);
            }
        }

        public TimeSlicedMonoRunner(string name, float maxMilliseconds, bool mustSurvive = false)
        {
            _flushingOperation = new UnityCoroutineRunner.FlushingOperation();

            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            
            _info = new GreedyTimeBoundRunningInfo(maxMilliseconds) { runnerName = name };
            
            runnerBehaviour.StartUpdateCoroutine(new UnityCoroutineRunner.Process
                (_newTaskRoutines, _coroutines, _flushingOperation, _info));
        }

        class GreedyTimeBoundRunningInfo : UnityCoroutineRunner.RunningTasksInfo
        {
            public long maxMilliseconds;

            public GreedyTimeBoundRunningInfo(float maxMilliseconds)
            {
                this.maxMilliseconds = (long) (maxMilliseconds * 10000);
            }

            public override bool CanMoveNext(ref int index, int count, object current)
            {
                //never stops until maxMilliseconds is elapsed or Break.AndResumeNextIteration is returned
                if (index == count)
                    index = 0;

                if (_stopWatch.ElapsedTicks > maxMilliseconds || current == Break.AndResumeNextIteration)
                {
                    _stopWatch.Reset();
                    _stopWatch.Start();
                    
                    return false;
                }
                
                return true;
            }

            public override void Reset()
            {
                _stopWatch.Reset();
                _stopWatch.Start();
            }

            readonly Stopwatch _stopWatch = new Stopwatch();

        }

        readonly GreedyTimeBoundRunningInfo _info;
    }
}
#endif