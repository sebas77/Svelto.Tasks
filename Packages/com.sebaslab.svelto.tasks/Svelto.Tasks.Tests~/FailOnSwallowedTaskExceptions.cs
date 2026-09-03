using System;
using System.Collections.Generic;

namespace Svelto.Tasks.Tests
{
    /// <summary>
    /// Runners swallow exceptions thrown inside tasks: the exception is logged through
    /// Svelto.Console.onException, the task is marked Faulted and silently removed.
    /// Assertions executed inside a task body would therefore never fail a test: they
    /// throw, the runner eats the exception and the test keeps going green.
    ///
    /// Wrap the body of tests whose tasks contain assertions with this class:
    ///     using (new FailOnSwallowedTaskExceptions())
    ///     {
    ///         ...run tasks...
    ///     }
    /// Any swallowed task exception (and hence any failed in-task assertion) fails
    /// the test when disposed.
    ///
    /// Do NOT use it in tests that expect tasks to throw on purpose (e.g. invalid
    /// ExtraLean yield checks): those subscribe to Console.onException themselves.
    /// </summary>
    public sealed class FailOnSwallowedTaskExceptions : IDisposable
    {
        public FailOnSwallowedTaskExceptions()
        {
            Console.onException += RecordException;
        }

        void RecordException(Exception e, string message)
        {
            _swallowed.Add(new Exception($"{message} -> {e.GetType().Name}: {e.Message}", e));
        }

        public void Dispose()
        {
            Console.onException -= RecordException;

            if (_swallowed.Count > 0)
                Assert.Fail(
                    $"{_swallowed.Count} exception(s) were thrown inside tasks and swallowed by the runner " +
                    $"(the test would have silently passed). First one:\n{_swallowed[0]}");
        }

        readonly List<Exception> _swallowed = new List<Exception>();
    }
}
