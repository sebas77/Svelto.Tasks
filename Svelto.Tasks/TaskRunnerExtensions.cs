using System;
using Svelto.Tasks;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Lean;
using Svelto.Utilities;

namespace Svelto.Tasks.ExtraLean
{
    public static class TaskRunnerExtensions
    {
        public static void RunOn<TTask, TRunner>(this TTask enumerator, TRunner runner)
            where TTask : IEnumerator where TRunner : class, IInternalRunner<ExtraLeanSveltoTask<TTask>>
        {
            new ExtraLeanSveltoTask<TTask>().Run(runner, ref enumerator, false);
        }

        public static void Run(this IEnumerator enumerator)
        {
            new ExtraLeanSveltoTask<IEnumerator>()
               .Run((IInternalRunner<ExtraLeanSveltoTask<IEnumerator>>) StandardSchedulers.standardScheduler,
                      ref enumerator, true);
        }
    }
}

public static class TaskRunnerExtensions
{
    public static ContinuationEnumerator Run(this IEnumerator<TaskContract> enumerator)
    {
        return new LeanSveltoTask<IEnumerator<TaskContract>>()
           .Start((IInternalRunner<LeanSveltoTask<IEnumerator<TaskContract>>>) StandardSchedulers.standardScheduler,
                  ref enumerator, true);
    }
    
    public static ContinuationEnumerator Run<TTask, TRunner>(this TTask enumerator, TRunner runner) 
        where TTask:IEnumerator<TaskContract> where TRunner:class, IInternalRunner<LeanSveltoTask<TTask>>
    {
        return new LeanSveltoTask<TTask>().Start(runner, ref enumerator, false);
    }

    public static ContinuationEnumerator RunImmediate<TTask,  TRunner>(this TTask enumerator, TRunner runner) 
        where TTask: IEnumerator<TaskContract> where TRunner:class, IInternalRunner<LeanSveltoTask<TTask>>
    {
        return new LeanSveltoTask<TTask>().Start(runner, ref enumerator, true);
    }

    public static TaskContract Continue(this IEnumerator<TaskContract> enumerator)
    {
        return new TaskContract(enumerator);
    }

    public static TaskRoutine<TTask> ToTaskRoutine<TTask, TRunner>(this TTask enumerator, TRunner runner) where TTask: IEnumerator<TaskContract> where TRunner:IInternalRunner<TaskRoutine<TTask>>
    {
        var taskroutine = TaskRunner.AllocateNewTaskRoutine<TTask, TRunner>(runner);
        taskroutine.SetEnumerator(enumerator);
        return taskroutine;
    }
    
    public static void Complete<T>(this T enumerator, int _timeout = 0) where T:IEnumerator
    {
        var quickIterations = 0;

        if (_timeout > 0)
        {
            var then  = DateTime.Now.AddMilliseconds(_timeout);
            var valid = true;

            while (enumerator.MoveNext() && 
                   (valid = DateTime.Now < then)) ThreadUtility.Wait(ref quickIterations);

            if (valid == false)
                throw new Exception("synchronous task timed out, increase time out or check if it got stuck");
        }
        else
        {
            if (_timeout == 0)
                while (enumerator.MoveNext()) ThreadUtility.Wait(ref quickIterations);
            else
                while (enumerator.MoveNext());
        }
    }
    
    internal static void CompleteTask<T>(ref T enumerator, int _timeout = 0) where T:ISveltoTask
    {
        var quickIterations = 0;

        if (_timeout < 0)
            while (enumerator.MoveNext());
        else
        if (_timeout == 0)        
            while (enumerator.MoveNext()) ThreadUtility.Wait(ref quickIterations);
        else
        {
            var then  = DateTime.Now.AddMilliseconds(_timeout);
            var valid = true;

            while (enumerator.MoveNext() && 
                   (valid = DateTime.Now < then)) ThreadUtility.Wait(ref quickIterations);

            if (valid == false)
                throw new Exception("synchronous task timed out, increase time out or check if it got stuck");
        }
    }
}

