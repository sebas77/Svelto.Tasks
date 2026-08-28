using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class RunnerLifecycleTests
    {
        [Test]
        public void SteppableRunner_StopPreventsNewTasksFromStartingUntilSteppedAndUnstopped()
        {
            // What we are testing:
            // Stop() puts the runner in stopping state, blocking new tasks from being accepted immediately.
            // The runner can be stepped to flush tasks and then reused.

            using (var runner = new SteppableRunner("SteppableRunner_Stop"))
            {
                var counter = 0;

                IEnumerator<TaskContract> OneStepIncrement()
                {
                    counter++;
                    yield break;
                }
                
                runner.Stop();

                OneStepIncrement().RunOn(runner);

                Assert.That(runner.hasTasks, Is.True);
                
                runner.Step();

                Assert.That(counter, Is.EqualTo(0));
                
                OneStepIncrement().RunOn(runner);

                // After flushing, runner should allow new tasks.
                runner.Step();
                runner.Step();

                Assert.That(counter, Is.EqualTo(2));
            }
        }

        [Test]
        public void SteppableRunner_FlushClearsTasksAndAllowsReuse()
        {
            // What we are testing:
            // Flush() should stop and reset the runner's internal task state so it can be reused.

            using (var runner = new SteppableRunner("SteppableRunner_Flush"))
            {
                var counter = 0;

                IEnumerator<TaskContract> LongTask()
                {
                    counter++;
                    yield return TaskContract.Yield.It;
                    counter++;
                }

                LongTask().RunOn(runner);
                runner.Step();

                Assert.That(counter, Is.EqualTo(1));
                Assert.That(runner.hasTasks, Is.True);

                runner.Flush();

                Assert.That(runner.hasTasks, Is.False);

                LongTask().RunOn(runner);
                runner.Step();
                runner.Step();

                Assert.That(counter, Is.EqualTo(3));
            }
        }

        [Test]
        public void MultiThreadRunner_StopThenDispose_DoesNotDeadlock()
        {
            using var started = new ManualResetEventSlim(false);
            var runner = new MultiThreadRunner("MultiThreadRunner_StopDispose");

            IEnumerator<TaskContract> RunningTask()
            {
                started.Set();

                while (true)
                    yield return TaskContract.Yield.It;
            }

            RunningTask().RunOn(runner);
            Assert.That(started.Wait(2000), Is.True);

            runner.Stop();
            Assert.That(runner.WaitForTasksDone(2000), Is.True);

            Assert.DoesNotThrow(runner.Dispose);
        }

        [Test]
        public void MultiThreadRunner_FlushDisposesTasksAndKeepsWorkerReusable()
        {
            using var started = new ManualResetEventSlim(false);
            using var completed = new ManualResetEventSlim(false);
            using var runner = new MultiThreadRunner("MultiThreadRunner_FlushReuse");
            var disposed = false;
            var workerThreadBeforeFlush = 0;
            var workerThreadAfterFlush = 0;

            IEnumerator<TaskContract> RunningTask()
            {
                workerThreadBeforeFlush = Thread.CurrentThread.ManagedThreadId;
                started.Set();

                try
                {
                    while (true)
                        yield return TaskContract.Yield.It;
                }
                finally
                {
                    disposed = true;
                }
            }

            IEnumerator<TaskContract> TaskAfterFlush()
            {
                workerThreadAfterFlush = Thread.CurrentThread.ManagedThreadId;
                completed.Set();
                yield break;
            }

            RunningTask().RunOn(runner);
            Assert.That(started.Wait(2000), Is.True);

            runner.Flush();

            Assert.That(disposed, Is.True);
            Assert.That(runner.hasTasks, Is.False);

            TaskAfterFlush().RunOn(runner);

            Assert.That(completed.Wait(2000), Is.True);
            Assert.That(workerThreadAfterFlush, Is.EqualTo(workerThreadBeforeFlush));
        }

        [Test]
        public void MultiThreadRunner_FlushRejectsTaskSubmissionUntilResetCompletes()
        {
            using var enteredTask = new ManualResetEventSlim(false);
            using var releaseTask = new ManualResetEventSlim(false);
            using var runner = new MultiThreadRunner("MultiThreadRunner_FlushAdmission");
            Exception flushException = null;

            IEnumerator<TaskContract> BlockingTask()
            {
                enteredTask.Set();
                releaseTask.Wait();
                yield return TaskContract.Yield.It;
            }

            IEnumerator<TaskContract> RejectedTask()
            {
                yield break;
            }

            BlockingTask().RunOn(runner);
            Assert.That(enteredTask.Wait(2000), Is.True);

            var flushThread = new Thread(() =>
            {
                try
                {
                    runner.Flush();
                }
                catch (Exception exception)
                {
                    flushException = exception;
                }
            });

            flushThread.Start();

            var then = DateTime.UtcNow.AddMilliseconds(2000);
            while (runner.isStopping == false && DateTime.UtcNow < then)
                Thread.Yield();

            try
            {
                Assert.That(runner.isStopping, Is.True);
                Assert.Throws<MultiThreadRunnerException>(() => RejectedTask().RunOn(runner));
            }
            finally
            {
                releaseTask.Set();
            }

            Assert.That(flushThread.Join(2000), Is.True);
            Assert.That(flushException, Is.Null);
        }

        [Test]
        public void MultiThreadRunner_DoesNotExposeKill()
        {
            Assert.That(typeof(MultiThreadRunner).GetMethod("Kill"), Is.Null);
        }

        [Test]
        public void MultiThreadRunner_RejectsTaskSubmissionAfterDispose()
        {
            var runner = new MultiThreadRunner("MultiThreadRunner_DisposedAdmission");

            IEnumerator<TaskContract> RejectedTask()
            {
                yield break;
            }

            runner.Dispose();

            Assert.Throws<MultiThreadRunnerException>(() => RejectedTask().RunOn(runner));
        }

        [Test]
        public void MultiThreadRunner_DisposeWakesPausedWorkerAndDisposesQueuedTask()
        {
            var runner = new MultiThreadRunner("MultiThreadRunner_PausedDispose");
            var disposableTask = new DisposableEnumerator();

            runner.Pause();
            disposableTask.RunOn(runner);

            runner.Dispose();

            Assert.That(disposableTask.disposed, Is.True);
        }

        [Test]
        public void MultiThreadRunner_RejectsTaskSubmissionWhileDisposeWaitsForWorker()
        {
            using var enteredTask = new ManualResetEventSlim(false);
            using var releaseTask = new ManualResetEventSlim(false);
            var runner = new MultiThreadRunner("MultiThreadRunner_DisposeAdmission");
            Exception disposeException = null;

            IEnumerator<TaskContract> BlockingTask()
            {
                enteredTask.Set();
                releaseTask.Wait();
                yield return TaskContract.Yield.It;
            }

            IEnumerator<TaskContract> RejectedTask()
            {
                yield break;
            }

            BlockingTask().RunOn(runner);
            Assert.That(enteredTask.Wait(2000), Is.True);

            var disposeThread = new Thread(() =>
            {
                try
                {
                    runner.Dispose();
                }
                catch (Exception exception)
                {
                    disposeException = exception;
                }
            });

            disposeThread.Start();

            var then = DateTime.UtcNow.AddMilliseconds(2000);
            while (runner.isKilled == false && DateTime.UtcNow < then)
                Thread.Yield();

            try
            {
                Assert.That(runner.isKilled, Is.True);
                Assert.Throws<MultiThreadRunnerException>(() => RejectedTask().RunOn(runner));
            }
            finally
            {
                releaseTask.Set();
            }

            Assert.That(disposeThread.Join(2000), Is.True);
            Assert.That(disposeException, Is.Null);
        }

        [Test]
        public void MultiThreadRunner_FlushFromWorkerThreadThrowsAndRunnerRemainsUsable()
        {
            using var attempted = new ManualResetEventSlim(false);
            using var reused = new ManualResetEventSlim(false);
            using var runner = new MultiThreadRunner("MultiThreadRunner_WorkerFlush");
            Exception flushException = null;

            IEnumerator<TaskContract> FlushFromWorker()
            {
                try
                {
                    runner.Flush();
                }
                catch (Exception exception)
                {
                    flushException = exception;
                }

                attempted.Set();
                yield break;
            }

            IEnumerator<TaskContract> ReuseTask()
            {
                reused.Set();
                yield break;
            }

            FlushFromWorker().RunOn(runner);

            Assert.That(attempted.Wait(2000), Is.True);
            Assert.That(flushException, Is.TypeOf<MultiThreadRunnerException>());

            ReuseTask().RunOn(runner);
            Assert.That(reused.Wait(2000), Is.True);
        }

        [Test]
        public void MultiThreadRunner_DisposeFromWorkerThreadThrowsAndCanBeRetriedExternally()
        {
            using var attempted = new ManualResetEventSlim(false);
            var runner = new MultiThreadRunner("MultiThreadRunner_WorkerDispose");
            Exception disposeException = null;

            IEnumerator<TaskContract> DisposeFromWorker()
            {
                try
                {
                    runner.Dispose();
                }
                catch (Exception exception)
                {
                    disposeException = exception;
                }

                attempted.Set();
                yield break;
            }

            DisposeFromWorker().RunOn(runner);

            Assert.That(attempted.Wait(2000), Is.True);
            Assert.That(disposeException, Is.TypeOf<MultiThreadRunnerException>());
            Assert.DoesNotThrow(runner.Dispose);
            Assert.DoesNotThrow(runner.Dispose);
        }

        [Test]
        public void SteppableRunner_Stop_AllowsReuseAfterFlush_AndQueuedTasksDuringStopRunAfterward()
        {
            using (var runner = new SteppableRunner("SteppableRunner_StopReuse"))
            {
                var firstCounter  = 0;
                var secondCounter = 0;

                IEnumerator<TaskContract> TwoStepIncrementFirst()
                {
                    firstCounter++;
                    yield return TaskContract.Yield.It;
                    firstCounter++;
                }

                IEnumerator<TaskContract> TwoStepIncrementSecond()
                {
                    secondCounter++;
                    yield return TaskContract.Yield.It;
                    secondCounter++;
                }

                TwoStepIncrementFirst().RunOn(runner);

                runner.Step();
                Assert.That(firstCounter, Is.EqualTo(1));

                runner.Stop();

                TwoStepIncrementSecond().RunOn(runner);

                for (var i = 0; i < 32 && secondCounter < 2; i++)
                    runner.Step();

                Assert.That(secondCounter, Is.EqualTo(2));
            }
        }

        [Test]
        public void SteppableRunner_Kill_StopsAndPreventsReuse()
        {
            var runner = new SteppableRunner("SteppableRunner_Kill");

            var counter = 0;

            IEnumerator<TaskContract> Increment()
            {
                counter++;
                yield return TaskContract.Yield.It;
                counter++;
            }

            Increment().RunOn(runner);
            runner.Step();
            Assert.That(counter, Is.EqualTo(1));

            runner.Dispose();

            Assert.Throws<DBC.Tasks.PreconditionException>(() =>
            {
                Increment().RunOn(runner);
            });
        }

        [Test]
        public void SteppableRunner_Flush_DisposesRunningTasks()
        {
            var runner = new SteppableRunner("SteppableRunner_Flush_Dispose");

            var disposableTask = new DisposableEnumerator();

            disposableTask.RunOn(runner);

            // Start it
            runner.Step();

            Assert.That(runner.hasTasks, Is.True);

            runner.Flush();

            Assert.That(disposableTask.disposed, Is.True);
            Assert.That(runner.hasTasks, Is.False);

            runner.Dispose();
        }

        [Test]
        public void SteppableRunner_ResetFlushesTasksAndAllowsReuse()
        {
            using var runner = new SteppableRunner("SteppableRunner_Reset");
            var disposableTask = new DisposableEnumerator();
            var reused = false;

            disposableTask.RunOn(runner);
            runner.Step();

            runner.Reset();

            Assert.That(disposableTask.disposed, Is.True);
            Assert.That(runner.hasTasks, Is.False);

            IEnumerator<TaskContract> ReusedTask()
            {
                reused = true;
                yield break;
            }

            ReusedTask().RunOn(runner);
            runner.Step();

            Assert.That(reused, Is.True);
        }

        class DisposableEnumerator : IEnumerator<TaskContract>
        {
            public TaskContract Current => TaskContract.Yield.It;
            object System.Collections.IEnumerator.Current => Current;

            public bool MoveNext() => true;

            public void Reset() {}

            public void Dispose()
            {
                disposed = true;
            }

            public volatile bool disposed;
        }
    }
}
