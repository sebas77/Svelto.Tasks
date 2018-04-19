#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.DataStructures;
using Svelto.Tasks.Internal;

//SequentialMonoRunner doesn't execute the next
//coroutine in the queue until the previous one is completed

namespace Svelto.Tasks
{
    /// <summary>
    /// while you can istantiate a MonoRunner, you should use the standard one
    /// whenever possible. Istantiating multiple runners will defeat the
    /// initial purpose to get away from the Unity monobehaviours
    /// internal updates. MonoRunners are disposable though, so at
    /// least be sure to dispose of them once done
    /// </summary>
    public class SequentialMonoRunner : MonoRunner
    {
        public SequentialMonoRunner(string name)
        {
            UnityCoroutineRunner.InitializeGameObject(name, ref _go);

            var coroutines = new FasterList<IPausableTask>(NUMBER_OF_INITIAL_COROUTINE);
            var runnerBehaviour = _go.AddComponent<RunnerBehaviourUpdate>();
            var runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();

            _info = new UnityCoroutineRunner.RunningTasksInfo { runnerName = name };

            runnerBehaviour.StartUpdateCoroutine(UnityCoroutineRunner.Process
            (_newTaskRoutines, coroutines, _flushingOperation, _info,
                SequentialTasksFlushing,
                runnerBehaviourForUnityCoroutine, StartCoroutine));
        }

        protected override UnityCoroutineRunner.RunningTasksInfo info
        { get { return _info; } }

        protected override ThreadSafeQueue<IPausableTask> newTaskRoutines
        { get { return _newTaskRoutines; } }

        protected override UnityCoroutineRunner.FlushingOperation flushingOperation
        { get { return _flushingOperation; } }
        
        static void SequentialTasksFlushing(
            ThreadSafeQueue<IPausableTask> newTaskRoutines, 
            FasterList<IPausableTask> coroutines, 
            UnityCoroutineRunner.FlushingOperation flushingOperation)
        {
            if (newTaskRoutines.Count > 0 && coroutines.Count == 0)
                newTaskRoutines.DequeueInto(coroutines, 1);
        }

        readonly ThreadSafeQueue<IPausableTask>         _newTaskRoutines   = new ThreadSafeQueue<IPausableTask>();
        readonly UnityCoroutineRunner.FlushingOperation _flushingOperation = new UnityCoroutineRunner.FlushingOperation();
        readonly UnityCoroutineRunner.RunningTasksInfo  _info;
      
        const int NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}
#endif