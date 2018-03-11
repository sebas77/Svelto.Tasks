using Svelto.Tasks;
using System.Collections;
using Svelto.Utilities;

public static class TaskRunnerExtensions
{
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
    
    public static void Complete(this IEnumerator enumerator)
    {
        while (enumerator.MoveNext()) ThreadUtility.Yield();
    }
}

