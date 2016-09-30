
using System.Collections;

namespace Svelto.Tasks
{
    public static class TaskRunnerExtensions
    {
        public static void RunOnSchedule(this IEnumerable enumerable, IRunner runner)
        {
            enumerable.GetEnumerator().RunOnSchedule(runner);
        }

        public static void Run(this IEnumerable enumerable)
        {
            enumerable.GetEnumerator().Run();
        }

        public static void RunOnSchedule(this IEnumerator enumerator, IRunner runner)
        {
            TaskRunner.Instance.RunOnSchedule(runner, enumerator);
        }

        public static void Run(this IEnumerator enumerator)
        {
            TaskRunner.Instance.Run(enumerator);
        }

        public static ITaskRoutine PrepareOnSchedule(this IEnumerable enumerable, IRunner runner)
        {
            return enumerable.GetEnumerator().PrepareOnSchedule(runner);
        }

        public static ITaskRoutine PrepareOnSchedule(this IEnumerator enumerator, IRunner runner)
        {
            return TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(runner).SetEnumerator(enumerator);
        }
    }
}
