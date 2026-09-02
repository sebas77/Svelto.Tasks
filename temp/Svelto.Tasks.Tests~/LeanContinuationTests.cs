using System.Collections.Generic;
using Svelto.Tasks.Lean;
using Svelto.Tasks.Enumerators;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class LeanContinuationTests
    {
        [SetUp]
        public void Setup()
        {
            _taskRunner = new SteppableRunner("LeanSveltoStepRunner");
        }

        [Test]
        public void TestThatLeanTasksWaitForContinuesWhenRunnerListsResize()
        {
            //the assertions run inside task bodies: without this guard a failure would be
            //swallowed by the runner and the test would silently pass
            using (new FailOnSwallowedTaskExceptions())
            {
                // The task runner has an inner faster list that starts initialized with a private const value of 3.
                // 32 task continuations should be enough for the foreseeable future to execute a list resize.
                const int requiredTasks = 32;

                // Each task has a number, first it will continue with a task with number + 1.
                // Then it will wait for the sub task to finish, assert that the x value is number + 1 and finally set x to be number.
                // This means that when the 32th task (which has no continue) starts will find x with value 0 set it to 32
                // and then all the parent task will decrement the value by 1.
                int x = 0;
                IEnumerator<TaskContract> Task(int number)
                {
                    if (number < requiredTasks)
                    {
                        yield return Task(number + 1).Continue();
                        Assert.That(x, Is.EqualTo(number + 1), $"Task {number} did not wait its continuation task");
                    }
                    else
                    {
                        Assert.That(x, Is.EqualTo(0));
                    }

                    x = number;
                }

                Continuation task = Task(1).RunOn(_taskRunner);

                _taskRunner.WaitForTasksDoneRelaxed(1000);

                Assert.That(task.isRunning, Is.False, "The task did not complete in time");
            }
        }

        [TearDown]
        public void TearDown()
        {
            _taskRunner.Dispose();
        }

        SteppableRunner _taskRunner;
    }
}

