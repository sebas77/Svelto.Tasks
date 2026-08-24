using System.Collections.Generic;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class TaskCollectionTests
    {
        [Test]
        public void SerialTaskCollection_ExecutesTasksInOrder_AndIsReusable()
        {
            // What we are testing:
            // SerialTaskCollection runs tasks one after another (no overlap) and can be reused after Reset().
            var serial = new SerialTaskCollection("serial");

            var log = new List<int>();

            IEnumerator<TaskContract> TwoPhaseTask(int id)
            {
                log.Add(id); // start
                yield return TaskContract.Yield.It;
                log.Add(-id); // finish
            }

            serial.Add(TwoPhaseTask(1));
            serial.Add(TwoPhaseTask(2));
            serial.Add(TwoPhaseTask(3));

            serial.Complete(1000);

            Assert.That(log, Is.EqualTo(new[] { 1, -1, 2, -2, 3, -3 }));

            serial.Clear();
            log.Clear();

            serial.Add(TwoPhaseTask(4));
            serial.Add(TwoPhaseTask(5));

            using (var runner = new SyncRunner("sync2"))
            {
                serial.RunOn(runner);
                runner.WaitForTasksDoneRelaxed(1000);
            }

            Assert.That(log, Is.EqualTo(new[] { 4, -4, 5, -5 }));
        }

        [Test]
        public void ParallelTaskCollection_AllowsTasksToOverlap()
        {
            // What we are testing:
            // ParallelTaskCollection progresses tasks so that multiple tasks can be in-flight at the same time.

            var parallel = new ParallelTaskCollection("parallel", 4);

            var log = new List<int>();

            IEnumerator<TaskContract> TwoPhaseTask(int id)
            {
                log.Add(id); // start
                yield return TaskContract.Yield.It;
                log.Add(-id); // finish
            }

            parallel.Add(TwoPhaseTask(1));
            parallel.Add(TwoPhaseTask(2));

            // One MoveNext progresses all tasks once. With a parallel collection, both tasks can "start"
            // before either is resumed to "finish".
            parallel.MoveNext();

            Assert.That(log.Count, Is.EqualTo(2));
            Assert.That(log[0] > 0, Is.True);
            Assert.That(log[1] > 0, Is.True);

            parallel.Complete(1000);

            Assert.That(log, Is.EquivalentTo(new[] { 1, 2, -1, -2 }));
            Assert.That(log[0] > 0, Is.True);
            Assert.That(log[1] > 0, Is.True);
        }

        [Test]
        public void TaskCollection_AddWhileRunning_Throws()
        {
            // What we are testing:
            // TaskCollection enforces that Add() can't be called while the collection is running.

            var serial = new SerialTaskCollection("serial");

            IEnumerator<TaskContract> YieldOnce()
            {
                yield return TaskContract.Yield.It;
            }

            serial.Add(YieldOnce());

            Assert.Throws<DBC.Tasks.PreconditionException>(() =>
            {
                // One MoveNext puts the collection in running state.
                serial.MoveNext();
                serial.Add(YieldOnce());
            });
        }

        [Test]
        public void TaskCollection_Clear_RemovesAllTasks()
        {
            // What we are testing:
            // Clear() empties the task collection.

            var serial = new SerialTaskCollection("serial");

            IEnumerator<TaskContract> Empty()
            {
                yield break;
            }

            serial.Add(Empty());
            serial.Add(Empty());

            serial.Clear();

            Assert.That(serial.Current, Is.EqualTo(default(TaskContract)));
        }

        [Test]
        public void SerialTaskCollection_Reset_AllowsReexecution()
        {
            // What we are testing:
            // SerialTaskCollection.Reset() resets the collection and its tasks so they can be run again.
            // Note: The tasks added must support Reset() (e.g. not compiler-generated iterators).

            var serial = new SerialTaskCollection("serial_reset");
            var task = new LeanEnumerator(2); // 2 iterations

            serial.Add(task);

            // Run first time
            serial.Complete(1000);
            Assert.That(task.AllRight, Is.True);

            // Reset
            serial.Reset();
            // LeanEnumerator.Reset() is called by SerialTaskCollection.Reset()
            Assert.That(task.iterations, Is.EqualTo(0));

            // Run second time
            serial.Complete(1000);
            Assert.That(task.AllRight, Is.True);
        }

        [Test]
        public void ParallelTaskCollection_Reset_AllowsReexecution()
        {
            // What we are testing:
            // ParallelTaskCollection.Reset() resets the collection and its tasks so they can be run again.
            // Note: The tasks added must support Reset().

            var parallel = new ParallelTaskCollection("parallel_reset", 2);
            var task1 = new LeanEnumerator(2);
            var task2 = new LeanEnumerator(2);

            parallel.Add(task1);
            parallel.Add(task2);

            // Run first time
            parallel.Complete(1000);
            Assert.That(task1.AllRight, Is.True);
            Assert.That(task2.AllRight, Is.True);

            // Reset
            parallel.Reset();
            Assert.That(task1.iterations, Is.EqualTo(0));
            Assert.That(task2.iterations, Is.EqualTo(0));

            // Run second time
            parallel.Complete(1000);
            Assert.That(task1.AllRight, Is.True);
            Assert.That(task2.AllRight, Is.True);
        }
    }
}
