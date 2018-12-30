using System.Collections.Generic;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    public class TaskRunner
    {
        /// <summary>
        /// Use this function only to preallocate TaskRoutine that can be reused. this minimize run-time allocations
        /// </summary>
        /// <returns>
        /// New reusable TaskRoutine
        /// </returns>
        public static TaskRoutine<T> AllocateNewTaskRoutine<T, W>(W runner) where T: IEnumerator<TaskContract> where W:IInternalRunner<TaskRoutine<T>>
        {
            return new TaskRoutine<T>(runner);
        }
        
        public static void StopAndCleanupAllDefaultSchedulers()
        {
            Lean.StandardSchedulers.KillSchedulers();
            ExtraLean.StandardSchedulers.KillSchedulers();
        }

        static TaskRunner()
         {
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

        public static void Pause()
        {
            Lean.StandardSchedulers.Pause();
            ExtraLean.StandardSchedulers.Pause();
        }

        public static void Resume()
        {
            Lean.StandardSchedulers.Resume();
            ExtraLean.StandardSchedulers.Resume();
        }
    }
}
