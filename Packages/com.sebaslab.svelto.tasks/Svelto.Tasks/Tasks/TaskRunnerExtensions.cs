using System;
using Svelto.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Utilities;

namespace Svelto.Tasks.ExtraLean
{
    public static class TaskRunnerExtensions
    {
        public static void RunOn<TTask, TRunner>(this TTask enumerator, TRunner runner)
                where TTask : struct, IEnumerator where TRunner : class, IRunner<Struct.ExtraLeanSveltoTask<TTask>>
        {
            new Struct.ExtraLeanSveltoTask<TTask>().Run(runner, ref enumerator);
        }

        public static void RunOn<TRunner>(this IEnumerator enumerator, TRunner runner)
                where TRunner : class, IRunner<ExtraLeanSveltoTask<IEnumerator>>
        {
            new ExtraLeanSveltoTask<IEnumerator>().Run(runner, ref enumerator);
        }
    }

    public static class TaskRunnerExtensionsRef
    {
        public static void RunOn<TTask, TRunner>(this TTask enumerator, TRunner runner)
                where TTask : class, IEnumerator where TRunner : class, IRunner<ExtraLeanSveltoTask<TTask>>
        {
            new ExtraLeanSveltoTask<TTask>().Run(runner, ref enumerator);
        }
    }
}

namespace Svelto.Tasks.Lean
{
    public static class TaskRunnerExtensions
    {
        public static Continuation RunOn<TTask, TRunner>(this TTask enumerator, TRunner runner)
                where TTask : struct, IEnumerator<TaskContract> where TRunner : class, IRunner<LeanSveltoTask<TTask>>
        {
            return new LeanSveltoTask<TTask>().Run(runner, enumerator);
        }

        public static Continuation RunOn<TRunner>(this IEnumerator<TaskContract> enumerator, TRunner runner)
                where TRunner : class, IRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
        {
            return new LeanSveltoTask<IEnumerator<TaskContract>>().Run(runner, enumerator);
        }

        public static async ValueTask<T> ToTask<T>(this IEnumerator<TaskContract> iteratorBlock, IGenericLeanRunner runner)
                where T : class //TaskContract cannot hold a generic type so this constraint is to avoid risking a runtime boxing
        {
            var continuation = iteratorBlock.RunOn(runner);

            while (continuation.isRunning)
            {
                await Task.Yield();
            }

            return iteratorBlock.Current.ToRef<T>();
        }
    }
}

public static class TaskRunnerExtensions
{
    public static TaskContract Continue<T>(this T enumerator)
            where T : class, IEnumerator //TaskContract cannot hold a generic type so this constraint is to avoid risking a runtime boxing 
    {
        if (enumerator is IEnumerator<TaskContract> == true)
            return new TaskContract((IEnumerator<TaskContract>) enumerator);

        return new TaskContract(enumerator);
    }

    //if task is continued with forget, the task will be executed, but the caller will not wait for it to complete
    //this method is used when you want to run, but not wait, a task on the same runner of the parent without knowing what it was

    public static TaskContract Forget<T>(this T task)
            where T : class, IEnumerator<TaskContract> //TaskContract cannot hold a generic type so this constraint is to avoid risking a runtime boxing 
    {
        return new TaskContract(task, true);
    }

    public static bool WaitForTasksDone<T>(this MultiThreadRunner<T> runner, int _timeout = 0) where T : ISveltoTask
    {
        var quickIterations = 0;

        if (_timeout > 0)
        {
            var  then  = DateTime.UtcNow.AddMilliseconds(_timeout);

            while (runner.hasTasks && DateTime.UtcNow < then)
            {
                ThreadUtility.TakeItEasy();
            }

            return runner.hasTasks == false;
        }
        else
        {
            while (runner.hasTasks)
            {
                ThreadUtility.Wait(ref quickIterations);
            }
            
            return true;
        }
    }

    public static bool WaitForTasksDone<T>(this T runner, int frequency, int _timeout = 0) where T : ISteppableRunner
    {
        var quickIterations = 0;

        if (_timeout > 0)
        {
            var  then   = DateTime.UtcNow.AddMilliseconds(_timeout);
            var  valid  = true;
            bool isDone = false;

            while (isDone == false && valid == true)
            {
                valid  = DateTime.UtcNow < then;
                runner.Step();
                isDone = runner.hasTasks == false;
                ThreadUtility.Wait(ref quickIterations, frequency);
            }

            if (valid == false && isDone == false)
                return false;
        }
        else
        {
            if (_timeout == 0)
            {
                bool isDone = false;

                while (isDone == false)
                {
                    runner.Step();
                    isDone = runner.hasTasks == false;
                    ThreadUtility.Wait(ref quickIterations, frequency);
                }
            }
            else
            {
                //careful, a tight loop may prevent other thread from running as it would take 100% of the core
                bool isDone = false;

                while (isDone == false)
                {
                    runner.Step();
                    isDone = runner.hasTasks == false;
                }
            }
        }

        return true;
    }

    public static bool WaitForTasksDoneRelaxed<T>(this T runner, int _timeout = 0) where T : ISteppableRunner
    {
        if (_timeout > 0)
        {
            var  then   = DateTime.UtcNow.AddMilliseconds(_timeout);
            var  valid  = true;
            bool isDone = false;

            while (isDone == false && valid == true)
            {
                valid = DateTime.UtcNow < then;
                runner.Step();
                isDone = runner.hasTasks == false;
                ThreadUtility.Relax();
            }

            if (valid == false && isDone == false)
                return false;
        }
        else
        {
            if (_timeout == 0)
            {
                bool isDone = false;

                while (isDone == false)
                {
                    runner.Step();
                    isDone = runner.hasTasks == false;
                    ThreadUtility.Relax();
                }
            }
            else
            {
                throw new ArgumentException();
            }
        }

        return true;
    }

    public static void Complete(this IEnumerator<TaskContract> task, int MSTimeOut = 0)
    {
        var syncRunnerValue = Svelto.Tasks.Lean.LocalSyncRunners.syncRunner.Value;
        Svelto.Tasks.Lean.TaskRunnerExtensions.RunOn(task, syncRunnerValue);
        syncRunnerValue.WaitForTasksDoneRelaxed(MSTimeOut);
    }

    public static void Complete<T>(this T enumerator, int MSTimeOut = 0)  where T : class, IEnumerator
    {
        var quickIterations = 0;

        if (MSTimeOut > 0)
        {
            var  then   = DateTime.UtcNow.AddMilliseconds(MSTimeOut);
            var  valid  = true;
            bool isDone = false;

            while (isDone == false && valid == true)
            {
                valid  = DateTime.UtcNow       < then;
                isDone = enumerator.MoveNext() == false;
                ThreadUtility.Wait(ref quickIterations);
            }

            if (valid == false && isDone == false)
                throw new SveltoTaskException("synchronous task timed out, increase time out or check if it got stuck");
        }
        else
        {
            if (MSTimeOut == 0)
                while (enumerator.MoveNext())
                    ThreadUtility.Wait(ref quickIterations);
            else //careful, a tight loop may prevent other thread from running as it would take 100% of the core
                while (enumerator.MoveNext())
                    ;
        }
    }

    public static void Complete<T>(this ref T enumerator, int MSTimeOut = 0) where T : struct, IEnumerator
    {
        var quickIterations = 0;

        if (MSTimeOut > 0)
        {
            var  then   = DateTime.UtcNow.AddMilliseconds(MSTimeOut);
            var  valid  = true;
            bool isDone = false;

            while (isDone == false && valid == true)
            {
                valid  = DateTime.UtcNow       < then;
                isDone = enumerator.MoveNext() == false;
                ThreadUtility.Wait(ref quickIterations);
            }

            if (valid == false && isDone == false)
                throw new SveltoTaskException("synchronous task timed out, increase time out or check if it got stuck");
        }
        else
        {
            if (MSTimeOut == 0)
                while (enumerator.MoveNext())
                    ThreadUtility.Wait(ref quickIterations);
            else //careful, a tight loop may prevent other thread from running as it would take 100% of the core
                while (enumerator.MoveNext()) ;
        }
    }
}