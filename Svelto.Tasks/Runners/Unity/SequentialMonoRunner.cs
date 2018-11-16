#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.DataStructures;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    //SequentialMonoRunner doesn't execute the next
    //coroutine in the queue until the previous one is completed
    /// </summary>
    public class SequentialMonoRunner : MonoRunner
    {
        public SequentialMonoRunner(string name, bool mustSurvive = false):base(name)
        {
            UnityCoroutineRunner.InitializeGameObject(name, ref _go, mustSurvive);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            var info = new UnityCoroutineRunner.RunningTasksInfo { runnerName = name };

            runnerBehaviour.StartUpdateCoroutine(new UnityCoroutineRunner.Process<UnityCoroutineRunner.RunningTasksInfo>
            (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }

        static void SequentialTasksFlushing(
            ThreadSafeQueue<IPausableTask> newTaskRoutines, 
            FasterList<IPausableTask> coroutines, 
            UnityCoroutineRunner.FlushingOperation flushingOperation)
        {
            if (newTaskRoutines.Count > 0 && coroutines.Count == 0)
                newTaskRoutines.DequeueInto(coroutines, 1);
        }
    }
}
#endif