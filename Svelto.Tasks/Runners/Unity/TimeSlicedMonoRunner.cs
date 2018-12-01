#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using System.Diagnostics;
using Svelto.DataStructures;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    //TimeSlicedMonoRunner ensures that the tasks running run up to the maxMilliseconds time.
    //If a task takes less than it, the next one will be executed and so on until maxMilliseconds is reached.
    //TimeSlicedMonoRunner can work with one task only too, this means that it would force the task to run up
    //to maxMilliseconds per frame, unless this returns Break.AndResumeIteration.
    /// </summary>
    public class TimeSlicedMonoRunner : MonoRunner
    {
        public float maxMilliseconds
        {
            set
            {
                _info.maxTicks = (long) (value * 10000);
            }
        }

        public TimeSlicedMonoRunner(string name, float maxMilliseconds, bool mustSurvive = false):base(name)
        {
            _flushingOperation = new UnityCoroutineRunner.FlushingOperation();

            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            
            _info = new TimeSlicedRunningInfo(maxMilliseconds, _coroutines) { runnerName = name };
            
            runnerBehaviour.StartUpdateCoroutine(new UnityCoroutineRunner.Process<TimeSlicedRunningInfo>
                (_newTaskRoutines, _coroutines, _flushingOperation, _info));
        }

        class TimeSlicedRunningInfo : IRunningTasksInfo
        {
            public long maxTicks;

            public TimeSlicedRunningInfo(float maxMilliseconds, FasterList<IPausableTask> coroutines)
            {
                this.maxTicks = (long) (maxMilliseconds * 10000);
                _coroutines = coroutines;
                _stopWatch = new Stopwatch();
                runnerName = "GreedyTimeBoundrunningInfo";
            }

            public bool CanMoveNext(ref int nextIndex, TaskCollection<IEnumerator>.CollectionTask currentResult)
            {
                //never stops until maxMilliseconds is elapsed or Break.AndResumeNextIteration is returned
                if (_stopWatch.ElapsedTicks > maxTicks)
                {
                    _stopWatch.Reset();
                    _stopWatch.Start();
                    
                    return false;
                }

                if (nextIndex >= _coroutines.Count)
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
            readonly FasterList<IPausableTask> _coroutines;
        }

        TimeSlicedRunningInfo _info;
    }
}
#endif