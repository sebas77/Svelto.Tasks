using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks.ExtraLean;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class RunnerCapacityTests
    {
        [Test]
        public void SteppableRunner_WithInitialCapacity_RunsMoreConcurrentTasksThanDefault()
        {
            // What we are testing:
            // A runner constructed with an initial capacity sized to the expected number of
            // concurrent tasks must run them all correctly without growing its internal buffers.

            const int taskCount = 64;

            using (var runner = new Lean.SteppableRunner("SteppableRunner_Capacity", taskCount))
            {
                var completed = 0;

                for (int i = 0; i < taskCount; i++)
                    CountingTask(() => completed++).RunOn(runner);

                while (runner.hasTasks)
                    runner.Step();

                Assert.That(completed, Is.EqualTo(taskCount));
            }
        }

        [Test]
        public void SteppableRunner_WithInitialCapacity_SupportsSpawnedChildTasks()
        {
            // What we are testing:
            // Spawned (child) tasks live in the same preallocated containers, so capacity must
            // not interfere with Continue() chains spawned while tasks are running.

            using (var runner = new Lean.SteppableRunner("SteppableRunner_CapacityChildren", 4))
            {
                var childCompleted = false;
                var parentCompleted = false;

                IEnumerator<TaskContract> Parent()
                {
                    yield return Child().Continue();

                    parentCompleted = true;
                }

                IEnumerator<TaskContract> Child()
                {
                    childCompleted = true;

                    yield return TaskContract.Break.It;
                }

                Parent().RunOn(runner);

                while (runner.hasTasks)
                    runner.Step();

                Assert.That(childCompleted, Is.True);
                Assert.That(parentCompleted, Is.True);
            }
        }

        [Test]
        public void ExtraLeanSteppableRunner_WithInitialCapacity_RunsClassTasks()
        {
            // What we are testing:
            // The non-generic ExtraLean SteppableRunner exposes the same capacity parameter and
            // runs plain IEnumerator class tasks correctly with it.

            var runner = new ExtraLean.SteppableRunner("ExtraLeanRunner_Capacity", 16);

            var counter = new Counter();

            for (int i = 0; i < 16; i++)
                Svelto.Tasks.ExtraLean.TaskRunnerExtensions.RunOn(counter.Task(), runner);

            while (runner.hasTasks)
                runner.Step();

            Assert.That(counter.count, Is.EqualTo(16));

            runner.Dispose();
        }

        [Test]
        public void ExtraLeanGenericSteppableRunner_WithInitialCapacity_RunsStructTasks()
        {
            // What we are testing:
            // The generic ExtraLean SteppableRunner exposes the same capacity parameter and runs
            // concrete struct tasks correctly with it: struct tasks must progress in place inside
            // the preallocated containers, not mutate a copy.

            using (var runner = new ExtraLean.SteppableRunner<CapacityStructTask>(
                       "ExtraLeanRunner_StructCapacity", 16))
            {
                var counter = new Counter();

                for (int i = 0; i < 16; i++)
                    new CapacityStructTask(counter).RunOn(runner);

                while (runner.hasTasks)
                    runner.Step();

                Assert.That(counter.count, Is.EqualTo(16));
            }
        }

        [Test]
        public void MultiThreadRunner_WithInitialCapacity_RunsManyConcurrentTasks()
        {
            // What we are testing:
            // The same capacity parameter threaded through the MultiThreadRunner constructor
            // chain must reach the internal Process without breaking task execution.

            const int taskCount = 32;

            var runner = new Lean.MultiThreadRunner("MultiThreadRunner_Capacity",
                tightTasks: true, initialNumberOfTasks: taskCount);

            try
            {
                var counter = -1;

                for (int i = 0; i < taskCount; i++)
                    IncrementingTask(() => Interlocked.Increment(ref counter)).RunOn(runner);

                runner.WaitForTasksDone();

                Assert.That(counter, Is.EqualTo(taskCount - 1));
            }
            finally
            {
                runner.Dispose();
            }
        }

        class Counter
        {
            public int count;

            public IEnumerator Task()
            {
                count++;
                yield break;
            }
        }

        struct CapacityStructTask : IEnumerator
        {
            public CapacityStructTask(Counter counter) : this()
            {
                _counter = counter;
            }

            public object Current => null;

            public bool MoveNext()
            {
                _counter.count++;

                return false;
            }

            public void Reset() { }

            readonly Counter _counter;
        }

        static IEnumerator<TaskContract> CountingTask(System.Action onDone)
        {
            onDone();
            yield return TaskContract.Break.It;
        }

        static IEnumerator<TaskContract> IncrementingTask(System.Action onDone)
        {
            onDone();
            yield return TaskContract.Break.It;
        }
    }
}
