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
        public TimeSlicedMonoRunner(string name, float maxMilliseconds, bool mustSurvive = false)
        {
            _flushingOperation = new UnityCoroutineRunner.FlushingOperation();

            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            var runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();
            UnityCoroutineRunner.RunningTasksInfo info;
            
            info = new GreedyTimeBoundRunningInfo(maxMilliseconds) { runnerName = name };
            
            runnerBehaviour.StartUpdateCoroutine(new UnityCoroutineRunner.Process
                (_newTaskRoutines, _coroutines, _flushingOperation, info,
                 UnityCoroutineRunner.StandardTasksFlushing,
                 runnerBehaviourForUnityCoroutine, StartCoroutine));
        }

        class GreedyTimeBoundRunningInfo : UnityCoroutineRunner.RunningTasksInfo
        {
            public GreedyTimeBoundRunningInfo(float maxMilliseconds)
            {
                _maxMilliseconds = maxMilliseconds;
            }

            public override bool MoveNext(ref int index, int count)
            {
                if (index == 0)
                {
                    _stopWatch.Reset();
                    _stopWatch.Start();
                }

                if (_stopWatch.ElapsedMilliseconds > _maxMilliseconds)
                    return false;

                if (index == count)
                    index = 0;
                
                return true;
            }

            readonly Stopwatch _stopWatch = new Stopwatch();
            readonly float     _maxMilliseconds;

        }
    }
}
#endif