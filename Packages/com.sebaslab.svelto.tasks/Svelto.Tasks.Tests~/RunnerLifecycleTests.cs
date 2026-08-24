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
            // What we are testing:
            // MultiThreadRunner can be stopped and disposed without deadlocking even with running tasks.

            using (var runner = new MultiThreadRunner("MultiThreadRunner_StopDispose"))
            {
                IEnumerator<TaskContract> SpinYield(int iterations)
                {
                    var i = 0;
                    while (i++ < iterations)
                    {
                        yield return TaskContract.Yield.It;
                    }
                }

                SpinYield(64).RunOn(runner);
                
                // Let it run a bit
                Thread.Sleep(10);
                
                runner.Stop();
                // Dispose is called by using block
            }
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
