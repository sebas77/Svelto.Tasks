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
    public static ContinuationWrapper RunOnScheduler(this IEnumerator enumerator, IRunner runner)
    {
        return TaskRunner.Instance.RunOnScheduler(runner, enumerator);
    }

    public static ContinuationWrapper Run(this IEnumerator enumerator)
    {
        return TaskRunner.Instance.Run(enumerator);
    }
    
    public static void Complete(this IEnumerator enumerator)
    {
        var quickIterations = 0;
        
        while (enumerator.MoveNext())
            ThreadUtility.Wait(ref quickIterations);
    }
}

