using System.Collections.Generic;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Lean;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks
{
    public class TaskRunner
    {
        static TaskRunner _instance;

        public static TaskRunner Instance
        {
            get
            {
                if (_instance == null)
                    InitInstance();

                return _instance;
            }
        }

        /// <summary>
        /// Use this function only to preallocate TaskRoutine that can be reused. this minimize run-time allocations
        /// </summary>
        /// <returns>
        /// New reusable TaskRoutine
        /// </returns>
        public ITaskRoutine<IEnumerator<TaskContract>> AllocateNewTaskRoutine()
        {
            return new TaskRoutine<IEnumerator<TaskContract>>((IInternalRunner<TaskRoutine<IEnumerator<TaskContract>>>) StandardSchedulers.standardScheduler);
        }
        
        public TaskRoutine<T> AllocateNewTaskRoutine<T, W>(W runner) where T: IEnumerator<TaskContract> where W:IInternalRunner<TaskRoutine<T>>
        {
            return new TaskRoutine<T>(runner);
        }
        
        public ITaskRoutine<IEnumerator<TaskContract>> AllocateNewTaskRoutine<W>(W runner) where W:IInternalRunner<TaskRoutine<IEnumerator<TaskContract>>>
        {
            return new TaskRoutine<IEnumerator<TaskContract>>(runner);
        }

        public static void StopAndCleanupAllDefaultSchedulers()
        {
            StandardSchedulers.KillSchedulers();
            ExtraLean.StandardSchedulers.KillSchedulers();

            _instance = null;
        }

        static void InitInstance()
         {
            _instance = new TaskRunner();

#if UNITY_EDITOR && TASKS_PROFILER_ENABLED
            var debugTasksObject = UnityEngine.GameObject.Find("Svelto.Tasks.Profiler");
            if (debugTasksObject == null)
            {
                debugTasksObject = new UnityEngine.GameObject("Svelto.Tasks.Profiler");
                debugTasksObject.gameObject.AddComponent<Svelto.Tasks.Profiler.TasksProfilerBehaviour>();
                UnityEngine.Object.DontDestroyOnLoad(debugTasksObject);
            }
#endif
        }
     }
}
