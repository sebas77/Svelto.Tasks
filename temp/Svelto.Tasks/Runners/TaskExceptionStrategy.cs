using System;
using System.Threading;

namespace Svelto.Tasks
{
    /// <summary>
    /// Receives exceptions from tasks that a runner has marked as faulted.
    /// Implementations can forward exceptions to an external reporting system. They must be thread-safe because
    /// multiple multithreaded runners can invoke the strategy concurrently. Exceptions escaping an implementation
    /// are ignored so a reporting failure cannot alter runner control flow.
    /// </summary>
    public interface ITaskExceptionStrategy
    {
        void HandleException(Exception exception);
    }

    /// <summary>
    /// Provides the mandatory exception strategy used by every Svelto task runner.
    /// </summary>
    public static class TaskExceptionStrategy
    {
        public static ITaskExceptionStrategy Current
        {
            get => Volatile.Read(ref _current);
            set => Volatile.Write(ref _current, value ?? throw new ArgumentNullException(nameof(value)));
        }

        internal static void HandleException(Exception exception)
        {
            try
            {
                Current.HandleException(exception);
            }
            catch (Exception strategyException)
            {
                // Exception reporting is external to task execution. A broken strategy must not prevent the runner
                // from disposing the faulted task and continuing with the remaining tasks.
                Console.LogException(strategyException,
                    "Task exception strategy failed while reporting a faulted task");
            }
        }

        static ITaskExceptionStrategy _current = LogTaskExceptionStrategy.Instance;
    }

    /// <summary>
    /// Preserves the standard Svelto.Tasks behavior by logging faulted-task exceptions and returning control to the
    /// runner, which disposes the faulted task and continues ticking.
    /// </summary>
    public sealed class LogTaskExceptionStrategy : ITaskExceptionStrategy
    {
        public static readonly LogTaskExceptionStrategy Instance = new LogTaskExceptionStrategy();

        LogTaskExceptionStrategy()
        {
        }

        public void HandleException(Exception exception)
        {
            Console.LogException(exception);
        }
    }
}
