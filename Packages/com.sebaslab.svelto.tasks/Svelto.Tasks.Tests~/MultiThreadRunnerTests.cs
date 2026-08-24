using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks.ExtraLean;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class MultiThreadRunnerTests
    {
        [Test]
        public void Lean_MultiThreadRunner_RunsTaskOnDifferentThread()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            int taskThreadId = -1;
            bool executed = false;

            IEnumerator<TaskContract> Task()
            {
                taskThreadId = Thread.CurrentThread.ManagedThreadId;
                executed = true;
                yield break;
            }

            using (var runner = new Lean.MultiThreadRunner("LeanMultiThreadRunner"))
            {
                Task().RunOn(runner);

                Assert.That(runner.WaitForTasksDone(1000), Is.True);
            }

            Assert.That(executed, Is.True);
            Assert.That(taskThreadId, Is.Not.EqualTo(-1));
            Assert.That(taskThreadId, Is.Not.EqualTo(mainThreadId));
        }

        [Test]
        public void ExtraLean_MultiThreadRunner_RunsTaskOnDifferentThread()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            int taskThreadId = -1;
            bool executed = false;

            IEnumerator Task()
            {
                taskThreadId = Thread.CurrentThread.ManagedThreadId;
                executed = true;
                yield break;
            }

            using (var runner = new ExtraLean.MultiThreadRunner("ExtraLeanMultiThreadRunner"))
            {
                Task().RunOn(runner);

                Assert.That(runner.WaitForTasksDone(1000), Is.True);
            }

            Assert.That(executed, Is.True);
            Assert.That(taskThreadId, Is.Not.EqualTo(-1));
            Assert.That(taskThreadId, Is.Not.EqualTo(mainThreadId));
        }

        [Test]
        public void TestMultithreadQuick()
        {
            var iterable1 = new LeanEnumerator(10000);
            using (var runner = new Lean.MultiThreadRunner("TestMultithreadQuick", false))
            {
                iterable1.RunOn(runner);

                Assert.That(runner.WaitForTasksDone(1000), Is.True);

                Assert.That(iterable1.AllRight == true);

                //do it again to test if starting another task works

                iterable1.Reset();

                iterable1.RunOn(runner);

                Assert.That(runner.WaitForTasksDone(1000), Is.True);

                Assert.That(iterable1.AllRight == true);
            }
        }

        [Test]
        public void TestMultithreadIntervaled()
        {
            using (var runner = new Lean.MultiThreadRunner("TestMultithreadIntervaled", 1u))
            {
                var iterable1 = new LeanEnumerator(2000);

                DateTime now = DateTime.Now;
                iterable1.RunOn(runner);
                Assert.That(runner.WaitForTasksDone(3000), Is.True);
                var seconds = (DateTime.Now - now).TotalSeconds;

                Assert.That((int) seconds, Is.EqualTo(2));
                Assert.That(iterable1.AllRight, Is.True);
            }
        }

        [Test]
        public void TestCrazyMultiThread()
        {
            ValueObject result = new ValueObject();

            var runner = new Lean.MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider");
            {
                int i = 0;
                while (i++ < 20)
                {
                    crazyEnumerator(result, runner).RunOn(runner);

                    Assert.That(runner.WaitForTasksDone(5000), Is.True);
                }
            }
            runner.Dispose();

            Assert.That(result.counter, Is.EqualTo(100));
        }

        IEnumerator<TaskContract> crazyEnumerator(ValueObject result, IRunner<LeanSveltoTask<IEnumerator<TaskContract>>> runner)
        {
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
            yield return SimpleEnumeratorFast(result).RunOn(runner);
        }

        IEnumerator<TaskContract> SimpleEnumeratorFast(ValueObject result)
        {
            yield return TaskContract.Yield.It;

            Interlocked.Increment(ref result.counter);
        }

        [Test]
        public void MultiThreadRunner_PausePreventsProgress_UntilResume()
        {
            using (var runner = new Lean.MultiThreadRunner("MT_PauseResume"))
            {
                var counter = 0;

                IEnumerator<TaskContract> Work()
                {
                    var i = 0;
                    while (i++ < 256)
                    {
                        counter++;
                        yield return TaskContract.Yield.It;
                    }
                }

                Work().RunOn(runner);

                runner.Pause();

                var snapshot = counter;
                var then = DateTime.UtcNow.AddMilliseconds(100);
                while (DateTime.UtcNow < then)
                {
                    Thread.Sleep(1);
                }

                Assert.That(counter, Is.EqualTo(snapshot));

                runner.Resume();

                Assert.That(runner.WaitForTasksDone(2000), Is.True);

                Assert.That(counter, Is.EqualTo(256));
            }
        }

        [Test]
        public void MultiThreadRunner_StopFlushesRunningTasks()
        {
            using (var runner = new Lean.MultiThreadRunner("MT_Stop"))
            {
                var start = Environment.TickCount;
                while (runner.isStarted == false)
                {
                    if (unchecked(Environment.TickCount - start) > 2000)
                        Assert.Fail("runner did not start");

                    Thread.Sleep(0);
                }

                var started = new ManualResetEventSlim(false);

                IEnumerator<TaskContract> Infinite()
                {
                    started.Set();

                    var i = 0;
                    while (i++ < 100000)
                        yield return TaskContract.Yield.It;
                }

                Infinite().RunOn(runner);

                Assert.That(started.Wait(2000), Is.True);

                runner.Stop();

                Assert.That(runner.WaitForTasksDone(2000), Is.True);

                Assert.That(runner.hasTasks, Is.False);
            }
        }

        class DisposableEnumerator : IEnumerator<TaskContract>
        {
            public DisposableEnumerator(ManualResetEventSlim started, int steps = 4)
            {
                _started = started;
                _steps = steps;
            }

            public DisposableEnumerator(int steps = 4)
            {
                _steps = steps;
            }

            public TaskContract Current => TaskContract.Yield.It;
            object IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (_started != null)
                    _started.Set();

                return ++count < _steps;
            }

            public void Reset()
            {
                count = 0;
                disposed = false;
            }

            public void Dispose()
            {
                disposed = true;
            }

            public volatile bool disposed = false;
            readonly ManualResetEventSlim _started;
            readonly int _steps;
            int count = 0;
        }

        [Test]
        public void MultiThreadRunner_DisposesTasksOnCompletion_NotOnlyOnDispose()
        {
            var disposableTask = new DisposableEnumerator();

            using (var runner = new Lean.MultiThreadRunner("MT_DisposesTasksOnCompletion"))
            {
                disposableTask.RunOn(runner);

                Assert.That(runner.WaitForTasksDone(2000), Is.True);
                Assert.That(runner.hasTasks, Is.False);

                Assert.That(disposableTask.disposed, Is.True);
            }
        }

        [Test]
        public void MultiThreadRunner_Dispose_DisposesQueuedTasks()
        {
            var disposableTask = new DisposableEnumerator();
            var runner = new Lean.MultiThreadRunner("MT_Dispose_DisposesQueuedTasks");
            disposableTask.RunOn(runner);

            var start = Environment.TickCount;
            while (runner.isStarted == false)
            {
                if (unchecked(Environment.TickCount - start) > 2000)
                    Assert.Fail("runner did not start");

                Thread.Sleep(0);
            }

            var started = new ManualResetEventSlim(false);
            var disposableTask2 = new DisposableEnumerator(started, 1024);
            disposableTask2.RunOn(runner);

            Assert.That(started.Wait(2000), Is.True);

            runner.Dispose();

            Assert.That(disposableTask.disposed, Is.True);
        }

        class DisposableExtraLeanEnumerator : IEnumerator, IDisposable
        {
            public object Current => null;
            public bool MoveNext() => ++count < 4;

            public void Reset()
            {
                count = 0;
                disposed = false;
            }

            public void Dispose() { disposed = true; }
            public bool disposed = false;
            int count = 0;
        }

        [Test]
        public void ExtraLean_MultiThreadRunner_DisposesTasksOnCompletion_NotOnlyOnDispose()
        {
            var disposableTask = new DisposableExtraLeanEnumerator();

            using (var runner = new ExtraLean.MultiThreadRunner("EL_MT_DisposesTasksOnCompletion"))
            {
                disposableTask.RunOn(runner);

                Assert.That(runner.WaitForTasksDone(2000), Is.True);
                Assert.That(runner.hasTasks, Is.False);

                Assert.That(disposableTask.disposed, Is.True);
            }
        }

        [Test]
        public void MultiThreadRunner_Stop_DisposesRunningTasks()
        {
            var started = new ManualResetEventSlim(false);
            var disposableTask = new DisposableEnumerator(started, 1024);

            using (var runner = new Lean.MultiThreadRunner("MT_Stop_DisposesRunningTasks"))
            {
                var start = Environment.TickCount;
                while (runner.isStarted == false)
                {
                    if (unchecked(Environment.TickCount - start) > 2000)
                        Assert.Fail("runner did not start");

                    Thread.Sleep(0);
                }

                disposableTask.RunOn(runner);

                Assert.That(started.Wait(2000), Is.True);

                runner.Stop();

                Assert.That(runner.WaitForTasksDone(2000), Is.True);
                Assert.That(disposableTask.disposed, Is.True);
            }
        }

        [Test]
        public void ExtraLean_MultiThreadRunner_Dispose_DisposesQueuedTasks()
        {
            var disposableTask = new DisposableExtraLeanEnumerator();
            var runner = new ExtraLean.MultiThreadRunner("EL_MT_Dispose_DisposesQueuedTasks");

            disposableTask.RunOn(runner);

            runner.Dispose();

            Assert.That(disposableTask.disposed, Is.True);
        }
    }
}

