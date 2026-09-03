using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class FlowModifiersTests
    {
        [Test]
        public void SerialFlow_RunsTasksSequentially()
        {
            using (var runner = new SteppableRunner("SerialFlow"))
            {
                runner.UseFlowModifier(new SerialFlow());

                var log = new List<int>();

                IEnumerator<TaskContract> Task1()
                {
                    log.Add(1);
                    yield return TaskContract.Yield.It;
                    log.Add(2);
                }

                IEnumerator<TaskContract> Task2()
                {
                    log.Add(3);
                    yield return TaskContract.Yield.It;
                    log.Add(4);
                }

                Task1().RunOn(runner);
                Task2().RunOn(runner);

                // Step 1: Task1 starts. Yields. SerialFlow stops iteration.
                runner.Step();
                Assert.That(log, Is.EqualTo(new[] { 1 }));

                // Step 2: Task1 continues. Completes. SerialFlow allows continuing to next task.
                runner.Step();
                Assert.That(log, Is.EqualTo(new[] { 1, 2, 3 }));

                // Step 3: Task2 continues.
                runner.Step();
                Assert.That(log, Is.EqualTo(new[] { 1, 2, 3, 4 }));
            }
        }

        [Test]
        public void StaggeredFlow_LimitsTasksPerTick_AndStarvesExcessTasks()
        {
            using (var runner = new SteppableRunner("StaggeredFlow"))
            {
                runner.UseFlowModifier(new StaggeredFlow(2)); //max 2 tasks processed per Step()

                var started  = new List<int>();
                var finished = new List<int>();

                IEnumerator<TaskContract> Task(int id)
                {
                    started.Add(id); //first execution
                    yield return TaskContract.Yield.It;
                    finished.Add(id); //second and last execution
                }

                Task(1).RunOn(runner);
                Task(2).RunOn(runner);
                Task(3).RunOn(runner);

                //Step 1: only the first two tasks are processed, task 3 doesn't run at all
                runner.Step();
                Assert.That(started, Is.EqualTo(new[] { 1, 2 }),
                    "no more than maxTasksPerIteration tasks can be processed per tick");

                //Step 2: exactly two more executions happen (one of them is task 3 finally
                //getting its turn, because completing task 1 freed a budget slot through the
                //swap-removal). Assert counts, not identities: the runner shuffles the queue
                //when tasks are removed
                int executionsAfterStep1 = started.Count + finished.Count;
                runner.Step();
                Assert.That(started.Count + finished.Count, Is.EqualTo(executionsAfterStep1 + 2),
                    "starved tasks must not get extra budget beyond maxTasksPerIteration");

                //whatever remains completes within the same per-tick limit...
                while (runner.hasTasks)
                    runner.Step();

                //...and nobody was dropped
                Assert.That(started, Is.EquivalentTo(new[] { 1, 2, 3 }));
                Assert.That(finished, Is.EquivalentTo(new[] { 1, 2, 3 }));
            }
        }

        [Test]
        public void TimeBoundFlow_BoundsWorkPerTick_ButDoesNotDropDeferredTasks()
        {
            using (var runner = new SteppableRunner("TimeBoundFlow"))
            {
                runner.UseFlowModifier(new TimeBoundFlow(20f)); //20ms budget per Step()

                int counter = 0;

                IEnumerator<TaskContract> SmallTask()
                {
                    Thread.Sleep(5); //5ms of work
                    counter++;
                    yield return TaskContract.Yield.It;
                }

                for (int i = 0; i < 10; i++)
                    SmallTask().RunOn(runner);

                //a single tick cannot process all the tasks within the budget
                runner.Step();
                Assert.That(counter, Is.GreaterThan(0), "the budget was never used");
                Assert.That(counter, Is.LessThan(10), "the budget was not enforced");

                //tasks that didn't fit in the budget are deferred, not dropped: each tick restarts
                //from the first task, so as soon as earlier tasks complete their slots free up and
                //the deferred ones get processed
                while (runner.hasTasks)
                    runner.Step();

                Assert.That(counter, Is.EqualTo(10), "deferred tasks must all eventually run");
            }
        }

        /// <summary>
        /// The feature distinguishing TimeSlicedFlow from TimeBoundFlow: when the end of the task
        /// list is reached while there is still time left in the slice, the iteration wraps back to
        /// the first task instead of ending the tick. Tasks are therefore revisited several times
        /// within a single Step(), which is impossible with any other flow modifier.
        /// </summary>
        [Test]
        public void TimeSlicedFlow_RevisitsAllTasksWithinASingleStep()
        {
            using (var runner = new SteppableRunner("TimeSlicedFlow"))
            {
                runner.UseFlowModifier(new TimeSlicedFlow(100f)); //100ms slice

                const int loopsPerTask = 5;
                int[] visits = new int[3];

                IEnumerator<TaskContract> SmallTask(int id)
                {
                    for (int i = 0; i < loopsPerTask; i++)
                    {
                        visits[id]++; //counts one body execution per visit
                        yield return TaskContract.Yield.It;
                    }
                }

                for (int i = 0; i < visits.Length; i++)
                    SmallTask(i).RunOn(runner);

                //a single tick: without wrapping it would perform exactly one pass (3 visits total,
                //one per pending task) and end the tick
                runner.Step();

                int totalVisits = visits[0] + visits[1] + visits[2];
                Assert.That(totalVisits, Is.GreaterThan(visits.Length),
                    "the iteration must wrap around and revisit tasks within the same tick");

                //the wrapped ticks keep cycling until every task completed its loop
                while (runner.hasTasks)
                    runner.Step();

                for (int i = 0; i < visits.Length; i++)
                    Assert.That(visits[i], Is.EqualTo(loopsPerTask),
                        $"task {i} was revisited but never dropped or duplicated");
            }
        }
    }
}
