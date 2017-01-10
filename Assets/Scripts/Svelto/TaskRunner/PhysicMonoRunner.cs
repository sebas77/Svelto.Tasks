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
        override public int numberOfRunningTasks { get { return _info.count; } }

        override protected ThreadSafeQueue<PausableTask> newTaskRoutines { get { return _newTaskRoutines; } }
        override protected FlushingOperation flushingOperation { get { return _flushingOperation; } }

        static PhysicMonoRunner()
        {
            if (_go == null)
            {
                _go = new GameObject("TaskRunner");

                Object.DontDestroyOnLoad(_go);
            }

            var coroutines = new FasterList<PausableTask>(NUMBER_OF_INITIAL_COROUTINE);

            RunnerBehaviourPhysic runnerBehaviour = _go.AddComponent<RunnerBehaviourPhysic>();
            runnerBehaviour.StartCoroutinePhysic(CoroutinesRunner(_newTaskRoutines, coroutines, _flushingOperation, _info));
        }

        override protected void StopUnityCouroutines()  {}

        readonly static ThreadSafeQueue<PausableTask> _newTaskRoutines = new ThreadSafeQueue<PausableTask>();
        readonly static FlushingOperation             _flushingOperation = new FlushingOperation();
        readonly static RunningTasksInfo              _info = new RunningTasksInfo();

        const int   NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}
