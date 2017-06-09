using Svelto.DataStructures;
using UnityEngine;

//
//it doesn't make any sense to have more than one MonoRunner active
//that's why I eventually decided to keep it as a static class.
//Only downside is that I assume that the TaskRunner gameobject
//is never destroyed after it's created.
//
namespace Svelto.Tasks.Internal
{
    class StaggeredMonoRunner : MonoRunner
    {
        static readonly RunnerBehaviour _runnerBehaviour;
        public override int numberOfRunningTasks { get { return _info.count; } }

        protected override ThreadSafeQueue<PausableTask> newTaskRoutines { get { return _newTaskRoutines; } }
        protected override FlushingOperation flushingOperation { get { return _flushingOperation; } }

        static StaggeredMonoRunner()
        {
            if (_go == null)
            {
                _go = new GameObject("TaskRunner");

                Object.DontDestroyOnLoad(_go);
            }

            _runnerBehaviour = _go.AddComponent<RunnerBehaviour>();
        }

        public StaggeredMonoRunner(int maxQueueLength)
        {
            _flushingOperation.framesLength = maxQueueLength;

            var coroutines = new FasterList<PausableTask>(NUMBER_OF_INITIAL_COROUTINE);

            _runnerBehaviour.StartCoroutine(CoroutinesRunner(_newTaskRoutines, coroutines, _flushingOperation, _info,
                NewFlushTasks));
        }

        protected static void NewFlushTasks(
            ThreadSafeQueue<PausableTask> newTaskRoutines, 
            FasterList<PausableTask> coroutines, FlushingOperation flushingOperation)
        {
            if (newTaskRoutines.Count > 0)
                newTaskRoutines.DequeueInto(coroutines, Mathf.CeilToInt(newTaskRoutines.Count / ((FlushingOperationStaggered) flushingOperation).framesLength));
        }

        protected class FlushingOperationStaggered:FlushingOperation
        {
            public int framesLength;
        }

        readonly FlushingOperationStaggered    _flushingOperation = new FlushingOperationStaggered();
        readonly RunningTasksInfo              _info = new RunningTasksInfo();
        readonly ThreadSafeQueue<PausableTask> _newTaskRoutines = new ThreadSafeQueue<PausableTask>();
    }
}
