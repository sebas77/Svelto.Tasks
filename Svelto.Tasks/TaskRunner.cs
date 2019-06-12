using System.Collections.Generic;

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
#if later         
        public static TaskRoutine<T> AllocateNewTaskRoutine<T, W>
            (W runner) where T : IEnumerator<TaskContract> where W : class, IRunner<TaskRoutine<T>>
        {
            return new TaskRoutine<T>(runner);
        }
#endif        
        public static void Dispose()
        {
            Lean.StandardSchedulers.Dispose();
            ExtraLean.StandardSchedulers.KillSchedulers();
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

        public static void Stop()
        {
            Lean.StandardSchedulers.Stop();
            ExtraLean.StandardSchedulers.StopAllCoroutines();
        }
    }
}
