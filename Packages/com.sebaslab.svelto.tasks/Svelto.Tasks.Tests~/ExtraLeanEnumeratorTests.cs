using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class ExtraLeanEnumeratorTests
    {
        //ExtraLean tasks are plain IEnumerators, but a Lean task can yield them through
        //an internal TaskContract constructor, which tests reach via reflection.
        static TaskContract AsExtraLean(IEnumerator extraLeanEnumerator)
        {
            var ctor = typeof(TaskContract).GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(IEnumerator) }, null);

            return (TaskContract)ctor.Invoke(new object[] { extraLeanEnumerator });
        }

        [Test]
        public void TestExtraLeanEnumerator_BreakAndStop_CompletesParentTask()
        {
            IEnumerator<TaskContract> ParentTask()
            {
                yield return AsExtraLean(ExtraLeanBreakAndStop());

                Assert.Fail("Parent task continued after Break.AndStop");
            }

            IEnumerator ExtraLeanBreakAndStop()
            {
                yield return TaskContract.Break.AndStop;
            }

            //the Assert.Fail above runs inside the task body: without this guard its
            //exception would be swallowed by the runner and the test would silently pass
            using (new FailOnSwallowedTaskExceptions())
            using (var runner = new SteppableRunner("ExtraLeanTest1"))
            {
                ParentTask().RunOn(runner);
                runner.Step(); //start parent, spawn the ExtraLean enumerator
                runner.Step(); //process it: Break.AndStop propagates and completes the parent

                Assert.That(runner.hasTasks, Is.False);
            }
        }

        [Test]
        public void TestExtraLeanEnumerator_BreakIt_ContinuesParentTask()
        {
            bool parentContinued = false;

            IEnumerator<TaskContract> ParentTask()
            {
                yield return AsExtraLean(ExtraLeanBreakIt());
                parentContinued = true;
            }

            IEnumerator ExtraLeanBreakIt()
            {
                yield return TaskContract.Break.It;
            }

            using (var runner = new SteppableRunner("ExtraLeanTest2"))
            {
                ParentTask().RunOn(runner);
                runner.Step(); //start parent
                runner.Step(); //process the ExtraLean enumerator: Break.It stops only the child...
                runner.Step(); //...and the parent resumes and finishes

                Assert.That(parentContinued, Is.True);
                Assert.That(runner.hasTasks, Is.False);
            }
        }

        [Test]
        public void TestExtraLeanEnumerator_InvalidReturn_FaultsTheTaskWithSveltoTaskException()
        {
            IEnumerator<TaskContract> ParentTask()
            {
                yield return AsExtraLean(ExtraLeanInvalid());
            }

            IEnumerator ExtraLeanInvalid()
            {
                yield return 123; //plain values are not valid yields for ExtraLean tasks
            }

            Exception caughtException = null;

            void OnException(Exception e, string msg)
            {
                caughtException = e;
            }

            using (var runner = new SteppableRunner("ExtraLeanTest3"))
            {
                ParentTask().RunOn(runner);

                //runners do not rethrow task exceptions: they log them through Console.onException,
                //mark the task Faulted and remove it
                Console.onException += OnException;

                try
                {
                    runner.Step();
                    runner.Step();
                }
                finally
                {
                    Console.onException -= OnException;
                }

                Assert.That(caughtException, Is.Not.Null, "no exception was reported for the invalid yield");
                Assert.That(caughtException.GetType().Name, Is.EqualTo("SveltoTaskException"));
                Assert.That(runner.hasTasks, Is.False, "the faulted task must be removed from the runner");
            }
        }
    }
}
