#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.DataStructures;
using Svelto.Tasks.Internal;

//StaggeredMonoRunner doesn't flush all the tasks at once, but it spread
//them over "framesToSpread" frames;

namespace Svelto.Tasks
{
    public class StaggeredMonoRunner : MonoRunner
    {
        public StaggeredMonoRunner(string name, int maxTasksPerFrame)
        {
            _flushingOperation = new FlushingOperationStaggered(maxTasksPerFrame);

            var go = UnityCoroutineRunner.InitializeGameobject(name);

            var coroutines = new FasterList<IPausableTask>(NUMBER_OF_INITIAL_COROUTINE);
            var runnerBehaviour = go.AddComponent<RunnerBehaviourUpdate>();
            var runnerBehaviourForUnityCoroutine = go.AddComponent<RunnerBehaviour>();

            _info = new UnityCoroutineRunner.RunningTasksInfo() { runnerName = name };

            runnerBehaviour.StartUpdateCoroutine(UnityCoroutineRunner.Process
                (_newTaskRoutines, coroutines, _flushingOperation, _info,
                 StaggeredTasksFlushing,
                 runnerBehaviourForUnityCoroutine, StartCoroutine));
        }

        protected override UnityCoroutineRunner.RunningTasksInfo info
        { get { return _info; } }

        protected override ThreadSafeQueue<IPausableTask> newTaskRoutines
        { get { return _newTaskRoutines; } }

        protected override UnityCoroutineRunner.FlushingOperation flushingOperation
        { get { return _flushingOperation; } }

        static void StaggeredTasksFlushing(
            ThreadSafeQueue<IPausableTask> newTaskRoutines, 
            FasterList<IPausableTask> coroutines, 
            UnityCoroutineRunner.FlushingOperation flushingOperation)
        {
            if (newTaskRoutines.Count > 0)
                newTaskRoutines.DequeueInto(coroutines, ((FlushingOperationStaggered)flushingOperation).maxTasksPerFrame);
        }

        public override void StartCoroutine(IPausableTask task)
        {
            paused = false;

            newTaskRoutines.Enqueue(task); //careful this could run on another thread!
        }

        class FlushingOperationStaggered:UnityCoroutineRunner.FlushingOperation
        {
            public readonly int maxTasksPerFrame;

            public FlushingOperationStaggered(int maxTasksPerFrame)
            {
                this.maxTasksPerFrame = maxTasksPerFrame;
            }
        }

        readonly FlushingOperationStaggered            _flushingOperation;
        readonly UnityCoroutineRunner.RunningTasksInfo _info;
        readonly ThreadSafeQueue<IPausableTask>        _newTaskRoutines = new ThreadSafeQueue<IPausableTask>();

        const int NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}
#endif