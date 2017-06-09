using Svelto.DataStructures;
using UnityEngine;
using Object = UnityEngine.Object;

//
//it doesn't make any sense to have more than one PhysicMonoRunner active
//that's why I eventually decided to keep it as a static class.
//Only downside is that I assume that the TaskRunner gameobject
//is never destroyed after it's created.
//
namespace Svelto.Tasks.Internal
{
    class PhysicMonoRunner : MonoRunner
    {
        public override int numberOfRunningTasks { get { return _info.count; } }

        protected override ThreadSafeQueue<PausableTask> newTaskRoutines { get { return _newTaskRoutines; } }
        protected override FlushingOperation flushingOperation { get { return _flushingOperation; } }

        static PhysicMonoRunner()
        {
            if (_go == null)
            {
                _go = new GameObject("TaskRunner");

                Object.DontDestroyOnLoad(_go);
            }

            var coroutines = new FasterList<PausableTask>(NUMBER_OF_INITIAL_COROUTINE);

            var runnerBehaviour = _go.AddComponent<RunnerBehaviourPhysic>();

            runnerBehaviour.StartPhysicCoroutine(CoroutinesRunner(_newTaskRoutines, coroutines, 
                _flushingOperation, _info, FlushTasks));
        }

        protected override void StopUnityCouroutines() {}

        static readonly ThreadSafeQueue<PausableTask> _newTaskRoutines = new ThreadSafeQueue<PausableTask>();
        static readonly FlushingOperation             _flushingOperation = new FlushingOperation();
        static readonly RunningTasksInfo              _info = new RunningTasksInfo();
    }
}
