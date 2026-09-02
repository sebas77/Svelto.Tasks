using System;
using System.Collections.Generic;
using NUnit.Framework;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class TaskExceptionStrategyTests
    {
        [Test]
        public void FaultedTask_IsReportedThroughStrategy_AndRunnerContinues()
        {
            var previousStrategy = TaskExceptionStrategy.Current;
            var strategy         = new RecordingExceptionStrategy();
            TaskExceptionStrategy.Current = strategy;

            try
            {
                using var runner = new SteppableRunner("ExceptionStrategyRunner");
                var healthyTaskRan = false;

                FaultingTask().RunOn(runner);
                HealthyTask().RunOn(runner);

                runner.Step();
                runner.Step();

                Assert.That(strategy.exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(healthyTaskRan, Is.True);
                Assert.That(runner.hasTasks, Is.False);

                IEnumerator<TaskContract> FaultingTask()
                {
                    yield return TaskContract.Yield.It;
                    throw new InvalidOperationException("expected failure");
                }

                IEnumerator<TaskContract> HealthyTask()
                {
                    healthyTaskRan = true;
                    yield break;
                }
            }
            finally
            {
                TaskExceptionStrategy.Current = previousStrategy;
            }
        }

        [Test]
        public void ThrowingExceptionStrategy_DoesNotInterruptFaultedTaskCleanup()
        {
            var previousStrategy = TaskExceptionStrategy.Current;
            TaskExceptionStrategy.Current = new ThrowingExceptionStrategy();
            Exception reportedException = null;

            void RecordException(Exception exception, string message)
            {
                reportedException = exception;
            }

            Console.onException += RecordException;

            try
            {
                using var runner = new SteppableRunner("ThrowingExceptionStrategyRunner");

                FaultingTask().RunOn(runner);

                runner.Step();
                Assert.DoesNotThrow(() => runner.Step());
                Assert.That(reportedException, Is.TypeOf<InvalidOperationException>());
                Assert.That(reportedException.Message, Is.EqualTo("reporting failed"));
                Assert.That(runner.hasTasks, Is.False);

                IEnumerator<TaskContract> FaultingTask()
                {
                    yield return TaskContract.Yield.It;
                    throw new InvalidOperationException("expected task failure");
                }
            }
            finally
            {
                Console.onException -= RecordException;
                TaskExceptionStrategy.Current = previousStrategy;
            }
        }

        sealed class RecordingExceptionStrategy : ITaskExceptionStrategy
        {
            public void HandleException(Exception exception)
            {
                this.exception = exception;
            }

            internal Exception exception;
        }

        sealed class ThrowingExceptionStrategy : ITaskExceptionStrategy
        {
            public void HandleException(Exception exception)
            {
                throw new InvalidOperationException("reporting failed");
            }
        }
    }
}
