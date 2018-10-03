#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Diagnostics;
using Svelto.Tasks.Internal.Unity;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    //TimeSlicedMonoRunner ensures that the tasks run up to the maxMilliseconds time. If a task takes less than it, the
    //next ones will be executed until maxMilliseconds is reached
    /// </summary>
    public class TimeSlicedMonoRunner : MonoRunner
    {
        public float maxMilliseconds
        {
            get
            {
                return _info.maxMilliseconds;
            }
            set
            {
                _info.maxMilliseconds = value;
            }
        }

        public TimeSlicedMonoRunner(string name, float maxMilliseconds, bool mustSurvive = false)
        {
            _flushingOperation = new UnityCoroutineRunner.FlushingOperation();

            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            var runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();
            
            _info = new GreedyTimeBoundRunningInfo(maxMilliseconds) { runnerName = name };
            
            runnerBehaviour.StartUpdateCoroutine(new UnityCoroutineRunner.Process
                (_newTaskRoutines, _coroutines, _flushingOperation, _info,
                 UnityCoroutineRunner.StandardTasksFlushing,
                 runnerBehaviourForUnityCoroutine, StartCoroutine));
        }

        class GreedyTimeBoundRunningInfo : UnityCoroutineRunner.RunningTasksInfo
        {
            public float maxMilliseconds;

            public GreedyTimeBoundRunningInfo(float maxMilliseconds)
            {
                this.maxMilliseconds = maxMilliseconds;
            }

            public override bool MoveNext(ref int index, int count)
            {
                if (index == 0)
                {
                    _stopWatch.Reset();
                    _stopWatch.Start();
                }

                if (_stopWatch.ElapsedMilliseconds > maxMilliseconds)
                    return false;

                if (index == count)
                    index = 0;
                
                return true;
            }

            readonly Stopwatch _stopWatch = new Stopwatch();

        }

        readonly GreedyTimeBoundRunningInfo _info;
    }
}
#endif