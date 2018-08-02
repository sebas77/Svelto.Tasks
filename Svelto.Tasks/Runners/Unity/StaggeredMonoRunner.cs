#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.DataStructures;
using Svelto.Tasks.Internal.Unity;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    //StaggeredMonoRunner doesn't flush all the tasks at once, but it spread
    //them over "framesToSpread" frames;
    /// </summary>
    public class StaggeredMonoRunner : MonoRunner
    {
        public StaggeredMonoRunner(string name, int maxTasksPerFrame, bool mustSurvive = false)
        {
            _flushingOperation = new FlushingOperationStaggered(maxTasksPerFrame);

            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            var runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();
            var info = new UnityCoroutineRunner.RunningTasksInfo { runnerName = name };

            runnerBehaviour.StartUpdateCoroutine(new UnityCoroutineRunner.Process
                (_newTaskRoutines, _coroutines, _flushingOperation, info,
                 StaggeredTasksFlushing,
                 runnerBehaviourForUnityCoroutine, StartCoroutine));
        }

        static void StaggeredTasksFlushing(
            ThreadSafeQueue<IPausableTask> newTaskRoutines, 
            FasterList<IPausableTask> coroutines, 
            UnityCoroutineRunner.FlushingOperation flushingOperation)
        {
            if (newTaskRoutines.Count > 0)
                newTaskRoutines.DequeueInto(coroutines, ((FlushingOperationStaggered)flushingOperation).maxTasksPerFrame);
        }

        class FlushingOperationStaggered:UnityCoroutineRunner.FlushingOperation
        {
            public readonly int maxTasksPerFrame;

            public FlushingOperationStaggered(int maxTasksPerFrame)
            {
                this.maxTasksPerFrame = maxTasksPerFrame;
            }
        }
    }
}
#endif