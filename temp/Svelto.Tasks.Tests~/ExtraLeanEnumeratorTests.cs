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
        public void TestExtraLeanEnumerator_BreakAndStop_CompletesEntireContinueChain()
        {
            bool rootContinued = false;
            bool middleContinued = false;

            IEnumerator<TaskContract> Root()
            {
                yield return Middle().Continue();
                rootContinued = true;
            }

            IEnumerator<TaskContract> Middle()
            {
                yield return AsExtraLean(ExtraLeanBreakAndStop());
                middleContinued = true;
            }

            IEnumerator ExtraLeanBreakAndStop()
            {
                yield return TaskContract.Break.AndStop;
            }

            using (var runner = new SteppableRunner("ExtraLeanChain"))
            {
                Root().RunOn(runner);

                while (runner.hasTasks)
                    runner.Step();
            }

            Assert.That(rootContinued, Is.False);
            Assert.That(middleContinued, Is.False);
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

        // ------------------------------------------------------------------
        // Disposal ownership of inline ExtraLean children (Lean parent wrapper)
        // ------------------------------------------------------------------

        enum ChildMode { Natural, BreakIt, BreakAndStop, WaitForever }

        sealed class CountingDisposableChild : IEnumerator, IDisposable
        {
            internal CountingDisposableChild(ChildMode mode)
            {
                _mode = mode;
            }

            public object Current
            {
                get
                {
                    switch (_mode)
                    {
                        case ChildMode.BreakIt:      return TaskContract.Break.It;
                        case ChildMode.BreakAndStop: return TaskContract.Break.AndStop;
                        default:                     return TaskContract.Yield.It;
                    }
                }
            }

            public bool MoveNext()
            {
                _step++;

                switch (_mode)
                {
                    case ChildMode.Natural:     return _step < 2; //second MoveNext completes naturally
                    case ChildMode.WaitForever: return true;      //pending until the test stops the runner task
                    default:                    return true;      //break modes signal through Current
                }
            }

            public void Reset() { }

            public void Dispose() => disposeCount++;

            internal int disposeCount;

            readonly ChildMode _mode;
            int _step;
        }

        [Test]
        public void InlineExtraLeanChild_NaturalCompletion_DisposesChildOnce()
        {
            var child = new CountingDisposableChild(ChildMode.Natural);
            bool parentResumed = false;

            IEnumerator<TaskContract> ParentTask()
            {
                yield return AsExtraLean(child);
                parentResumed = true;
            }

            using (var runner = new SteppableRunner("InlineChildNatural"))
            {
                ParentTask().RunOn(runner);

                while (runner.hasTasks)
                    runner.Step();

                Assert.That(parentResumed, Is.True);
            }

            Assert.That(child.disposeCount, Is.EqualTo(1),
                "a naturally completed inline child must be disposed once");
        }

        [Test]
        public void InlineExtraLeanChild_BreakIt_KeepsChildAlive()
        {
            var child = new CountingDisposableChild(ChildMode.BreakIt);
            bool parentResumed = false;

            IEnumerator<TaskContract> ParentTask()
            {
                yield return AsExtraLean(child);
                parentResumed = true;
            }

            using (var runner = new SteppableRunner("InlineChildBreakIt"))
            {
                ParentTask().RunOn(runner);

                while (runner.hasTasks)
                    runner.Step();

                Assert.That(parentResumed, Is.True);
            }

            Assert.That(child.disposeCount, Is.EqualTo(0),
                "Break.It keeps the state machine alive by contract: the child must not be disposed");
        }

        [Test]
        public void InlineExtraLeanChild_BreakAndStop_ChildDisposalOnTeardown()
        {
            var child = new CountingDisposableChild(ChildMode.BreakAndStop);

            IEnumerator<TaskContract> ParentTask()
            {
                yield return AsExtraLean(child);
            }

            using (var runner = new SteppableRunner("InlineChildAndStop"))
            {
                ParentTask().RunOn(runner);

                while (runner.hasTasks)
                    runner.Step();

                Assert.That(runner.hasTasks, Is.False);
            }

            Assert.That(child.disposeCount, Is.EqualTo(1),
                "teardown must deterministically release the abandoned inline child");
        }

        [Test]
        public void InlineExtraLeanChild_StoppedMidChild_ChildDisposalOnTeardown()
        {
            var child = new CountingDisposableChild(ChildMode.WaitForever);

            IEnumerator<TaskContract> ParentTask()
            {
                yield return AsExtraLean(child);
            }

            using (var runner = new SteppableRunner("InlineChildStop"))
            {
                ParentTask().RunOn(runner);

                runner.Step(); //start parent, spawn the child
                runner.Step(); //child yields Yield.It: still pending

                runner.Stop(); //abandon the task while the child is pending
                runner.Step(); //stopping pass: completes and disposes the task

                Assert.That(runner.hasTasks, Is.False);
            }

            Assert.That(child.disposeCount, Is.EqualTo(1),
                "teardown must deterministically release the abandoned inline child");
        }
    }
}
