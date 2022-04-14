using System;
using Svelto.Tasks;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Lean;
using Svelto.Utilities;

namespace Svelto.Tasks.ExtraLean
{
    public static class TaskRunnerExtensions
    {
        public static void RunOn<TTask, TRunner>(this TTask enumerator, TRunner runner)
            where TTask : IEnumerator where TRunner : class, IRunner<ExtraLeanSveltoTask<TTask>>
        {
            new ExtraLeanSveltoTask<TTask>().Run(runner, ref enumerator);
        }
        
        public static void RunOn<TRunner>(this IEnumerator enumerator, TRunner runner)
            where TRunner : class, IRunner<ExtraLeanSveltoTask<IEnumerator>>
        {
            new ExtraLeanSveltoTask<IEnumerator>().Run(runner, ref enumerator);
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
            return new LeanSveltoTask<TTask>().Run(runner, ref enumerator);
        }
        
        public static Continuation RunOn<TRunner>(this IEnumerator<TaskContract> enumerator, TRunner runner)
            where TRunner : class, IRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
        {
            return new LeanSveltoTask<IEnumerator<TaskContract>>().Run(runner, ref enumerator);
        }
    }
}

public static class TaskRunnerExtension2
{
    public static bool WaitForTasksDone<T>(this T runner, int frequency, int _timeout = 0) where T:ISteppableRunner 
    {
        var quickIterations = 0;

        if (_timeout > 0)
        {
            var  then   = DateTime.Now.AddMilliseconds(_timeout);
            var  valid  = true;
            bool isDone = false;

            while (isDone == false && valid == true)
            {
                valid  = DateTime.Now < then;
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
            {//careful, a tight loop may prevent other thread from running as it would take 100% of the core
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
    
    public static bool WaitForTasksDoneRelaxed<T>(this T runner, int _timeout = 0) where T:ISteppableRunner 
    {
        if (_timeout > 0)
        {
            var  then   = DateTime.Now.AddMilliseconds(_timeout);
            var  valid  = true;
            bool isDone = false;

            while (isDone == false && valid == true)
            {
                valid = DateTime.Now < then;
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
}

public static class TaskRunnerExtensions
{
    public static TaskContract Continue(this IEnumerator<TaskContract> task) 
    {
        return new TaskContract(task);
    }
    
    public static TaskContract Continue<T>(this T enumerator) where T:IEnumerator 
    {
        return new TaskContract(enumerator);
    }
    
    public static void Complete(this IEnumerator<TaskContract> task, int _timeout = 0) 
    {
        var syncRunnerValue = LocalSyncRunners<IEnumerator<TaskContract>>.syncRunner.Value;
        task.RunOn(syncRunnerValue);
        syncRunnerValue.ForceComplete(_timeout);
    }

    public static void Complete<T>(this T enumerator, int _timeout = 0) where T:IEnumerator 
    {
        var quickIterations = 0;

        if (_timeout > 0)
        {
            var  then   = DateTime.Now.AddMilliseconds(_timeout);
            var  valid  = true;
            bool isDone = false;

            while (isDone == false && valid == true)
            {
                valid  = DateTime.Now < then;
                isDone = enumerator.MoveNext();
                ThreadUtility.Wait(ref quickIterations);    
            }

            if (valid == false && isDone == false)
                throw new Exception("synchronous task timed out, increase time out or check if it got stuck");
        }
        else
        {
            if (_timeout == 0)
                while (enumerator.MoveNext())
                    ThreadUtility.Wait(ref quickIterations);
            else //careful, a tight loop may prevent other thread from running as it would take 100% of the core
                while (enumerator.MoveNext());
        }
    }
    
    public static void Complete(this Continuation enumerator, int _timeout = 1000)
    {
        var quickIterations = 0;

        if (_timeout > 0)
        {
            var then  = DateTime.Now.AddMilliseconds(_timeout);
            var valid = true;

            while (enumerator.isRunning &&
                   (valid = DateTime.Now < then)) ThreadUtility.Wait(ref quickIterations);

            if (valid == false)
                throw new Exception("synchronous task timed out, increase time out or check if it got stuck");
        }
        else
        {
            if (_timeout == 0)
                while (enumerator.isRunning)
                    ThreadUtility.Wait(ref quickIterations);
            else
                while (enumerator.isRunning);
        }
    }
}

