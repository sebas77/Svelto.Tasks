using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Svelto.DataStructures;
using Svelto.Tasks.Lean;
using Svelto.Tasks.Enumerators;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class AwaiterTests
    {
        [Test]
        public async Task TaskAwaiter_DoesNotBlockRunnerWhileTaskIsPending()
        {
            using (var runner = new SteppableRunner("NonBlockingTaskAwaiterTest"))
            {
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var unrelatedTaskRan = false;
                var operation = AwaitTask(gate.Task, runner);

                UnrelatedTask().RunOn(runner);

                var step = Task.Run(runner.Step);

                try
                {
                    Assert.That(step.Wait(500), Is.True,
                        "Runner.Step blocked while the awaited .NET Task was incomplete");
                    Assert.That(unrelatedTaskRan, Is.True);
                    Assert.That(operation.IsCompleted, Is.False);
                }
                finally
                {
                    gate.TrySetResult();
                    await step.WaitAsync(TimeSpan.FromSeconds(2));
                }

                Assert.That(PumpUntilComplete(runner, operation), Is.True);
                await operation;

                IEnumerator<TaskContract> UnrelatedTask()
                {
                    unrelatedTaskRan = true;
                    yield break;
                }
            }
        }

        [Test]
        public async Task ValueTaskAwaiter_DoesNotBlockRunnerWhileTaskIsPending()
        {
            using (var runner = new SteppableRunner("NonBlockingValueTaskAwaiterTest"))
            {
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var unrelatedTaskRan = false;
                var operation = AwaitValueTask(new ValueTask(gate.Task), runner);

                UnrelatedTask().RunOn(runner);

                var step = Task.Run(runner.Step);

                try
                {
                    Assert.That(step.Wait(500), Is.True,
                        "Runner.Step blocked while the awaited .NET ValueTask was incomplete");
                    Assert.That(unrelatedTaskRan, Is.True);
                    Assert.That(operation.IsCompleted, Is.False);
                }
                finally
                {
                    gate.TrySetResult();
                    await step.WaitAsync(TimeSpan.FromSeconds(2));
                }

                Assert.That(PumpUntilComplete(runner, operation), Is.True);
                await operation;

                IEnumerator<TaskContract> UnrelatedTask()
                {
                    unrelatedTaskRan = true;
                    yield break;
                }
            }
        }

        [Test]
        public async Task TaskAwaiter_ResumesOnRunnerAndPropagatesFault()
        {
            using (var runner = new SteppableRunner("TaskAwaiterFaultTest"))
            {
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var runnerThread = Thread.CurrentThread.ManagedThreadId;
                var continuationThread = 0;
                var operation = AwaitTask(gate.Task, runner, id => continuationThread = id);

                gate.SetException(new InvalidOperationException("expected"));

                Assert.That(PumpUntilComplete(runner, operation), Is.True);
                Assert.That(continuationThread, Is.EqualTo(runnerThread));
                Assert.That(async () => await operation, Throws.TypeOf<InvalidOperationException>()
                   .With.Message.EqualTo("expected"));
            }
        }

        [Test]
        public void TaskAwaiter_CompletedTaskContinuesSynchronously()
        {
            using (var runner = new SteppableRunner("CompletedTaskAwaiterTest"))
            {
                var currentThread = Thread.CurrentThread.ManagedThreadId;
                var continuationThread = 0;

                var operation = AwaitTask(Task.CompletedTask, runner, id => continuationThread = id);

                Assert.That(operation.IsCompletedSuccessfully, Is.True);
                Assert.That(continuationThread, Is.EqualTo(currentThread));
                Assert.That(runner.hasTasks, Is.False);
            }
        }

        [Test]
        public async Task TaskAwaiter_PropagatesCancellation()
        {
            using (var runner = new SteppableRunner("CancelledTaskAwaiterTest"))
            {
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var operation = AwaitTask(gate.Task, runner);

                gate.SetCanceled();

                Assert.That(PumpUntilComplete(runner, operation), Is.True);
                Assert.That(operation.IsCanceled, Is.True);
                Assert.That(async () => await operation, Throws.TypeOf<TaskCanceledException>());
            }
        }

        [Test]
        public void TaskAwaiter_InvalidRunnerLeavesOperationPending()
        {
            using (var runner = new RejectingRunner(false))
            {
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var operation = AwaitTask(gate.Task, runner);

                gate.SetResult();

                Assert.That(runner.validityChecked.Wait(2000), Is.True);
                Assert.That(runner.admissionAttempted.IsSet, Is.False);
                Assert.That(operation.IsCompleted, Is.False);
            }
        }

        [Test]
        public void LeanRootTask_RejectedAdmissionInvalidatesContinuation()
        {
            using (var runner = new RejectingRunner(true))
            {
                IEnumerator<TaskContract> Task()
                {
                    yield break;
                }

                Assert.That(() => Task().RunOn(runner), Throws.TypeOf<InvalidOperationException>());
                AssertRejectedContinuationWasInvalidated(runner.rejectedTask);
            }
        }

        class testClass
        {
            public bool continued = false;
        }
        [Test]
        public void TestThatSveltoAwaiterContinuationDoesNotRunWhenRunnerStops()
        {
            // arrange
            var runner = new SteppableRunner("SveltoAwaiterTest");
            
            var continued = new testClass();

            // obtain the custom awaiter that posts continuation into the runner
            var task = SomeAsyncOperation(continued, runner);
            
            while (continued.continued == false)
                runner.Step();
            
            // give some frames/time to potentially run (it shouldn't)
            new WaitForSecondsEnumerator(0.2f).Complete();

            Assert.That(task.IsCompleted, Is.False, "Continuation should not run when the runner is stopped");
        }

        async Task SomeAsyncOperation(testClass continued, SteppableRunner runner)
        {
            await Task.Delay(10).RunOn(runner);
            
            continued.continued = true;
            runner.Stop();
            await Task.Delay(10).RunOn(runner);
        }

        static async Task AwaitTask(Task task, IGenericLeanRunner runner, Action<int> resumed = null)
        {
            try
            {
                await task.RunOn(runner);
            }
            finally
            {
                resumed?.Invoke(Thread.CurrentThread.ManagedThreadId);
            }
        }

        static async Task AwaitValueTask(ValueTask task, SteppableRunner runner)
        {
            await task.RunOn(runner);
        }

        static bool PumpUntilComplete(SteppableRunner runner, Task operation)
        {
            var timeout = DateTime.UtcNow.AddSeconds(2);

            while (operation.IsCompleted == false && DateTime.UtcNow < timeout)
            {
                runner.Step();
                Thread.Yield();
            }

            return operation.IsCompleted;
        }

        static void AssertRejectedContinuationWasInvalidated(
            LeanSveltoTask<IEnumerator<TaskContract>> rejectedTask)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var field = typeof(LeanSveltoTask<IEnumerator<TaskContract>>).GetField("_continuation", flags);
            var continuation = (Continuation)field.GetValue(rejectedTask);

            Assert.That(SpinWait.SpinUntil(() => continuation.isRunning == false, 2000), Is.True,
                "Rejected task continuation was not returned to its pool");
        }

        sealed class RejectingRunner : IGenericLeanRunner
        {
            internal RejectingRunner(bool isValid)
            {
                _isValid = isValid;
            }

            public bool isValid
            {
                get
                {
                    validityChecked.Set();
                    return _isValid;
                }
            }

            public void AddTask(in LeanSveltoTask<IEnumerator<TaskContract>> task,
                (int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex) index)
            {
                rejectedTask = task;
                admissionAttempted.Set();
                throw new InvalidOperationException("Task admission rejected");
            }

            public void Dispose()
            {
                admissionAttempted.Dispose();
                validityChecked.Dispose();
            }

            internal readonly ManualResetEventSlim admissionAttempted = new ManualResetEventSlim(false);
            internal readonly ManualResetEventSlim validityChecked = new ManualResetEventSlim(false);
            internal LeanSveltoTask<IEnumerator<TaskContract>> rejectedTask;
            readonly bool _isValid;
        }
    }
}

