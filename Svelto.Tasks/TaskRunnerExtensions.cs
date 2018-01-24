
using Svelto.Tasks;
using System.Collections;

    public static class TaskRunnerExtensions
    {
        public static ContinuationWrapper RunOnSchedule(this IEnumerable enumerable, IRunner runner)
        {
            return enumerable.GetEnumerator().RunOnSchedule(runner);
        }

        public static ContinuationWrapper Run(this IEnumerable enumerable)
        {
            return enumerable.GetEnumerator().Run();
        }

        public static ContinuationWrapper ThreadSafeRunOnSchedule(this IEnumerable enumerable, IRunner runner)
        {
            return enumerable.GetEnumerator().ThreadSafeRunOnSchedule(runner);
        }

        public static ContinuationWrapper ThreadSafeRun(this IEnumerable enumerable)
        {
            return enumerable.GetEnumerator().ThreadSafeRun();
        }

        /// <summary>
        /// the first instructions until the first yield are executed immediately
        /// </summary>
        /// <param name="runner"></param>
        /// <param name="task"></param>
        /// <returns></returns>
        public static ContinuationWrapper RunOnSchedule(this IEnumerator enumerator, IRunner runner)
        {
            return TaskRunner.Instance.RunOnSchedule(runner, enumerator);
        }
        /// <summary>
        /// all the instructions are executed on the selected runner
        /// </summary>
        /// <param name="taskGenerator"></param>
        /// <returns></returns>
        public static ContinuationWrapper ThreadSafeRunOnSchedule(this IEnumerator enumerator, IRunner runner)
        {
            return TaskRunner.Instance.ThreadSafeRunOnSchedule(runner, enumerator);
        }

        public static ContinuationWrapper Run(this IEnumerator enumerator)
        {
            return TaskRunner.Instance.Run(enumerator);
        }

        public static ContinuationWrapper ThreadSafeRun(this IEnumerator enumerator)
        {
            return TaskRunner.Instance.ThreadSafeRun(enumerator);
        }

        public static ITaskRoutine PrepareTaskRoutineOnSchedule(this IEnumerable enumerable, IRunner runner)
        {
            return enumerable.GetEnumerator().PrepareTaskRoutineOnSchedule(runner);
        }

        public static ITaskRoutine PrepareTaskRoutineOnSchedule(this IEnumerator enumerator, IRunner runner)
        {
            return TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(runner).SetEnumerator(enumerator);
        }
    }

