using System.Collections.Generic;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class TaskBreakTests
    {
        [SetUp]
        public void Setup()
        {
            _iterable1 = new LeanEnumerator(10000);
            _iterable2 = new LeanEnumerator(10000);
        }

        [Test]
        public void TestThatAStandardBreakBreaksTheCurrentTaskOnly()
        {
            IEnumerator<TaskContract> severalTasksParent = SeveralTasksParent();
            severalTasksParent.Complete(1000); //ms
            
            Assert.That(_iterable1.AllRight, Is.True);
            Assert.That(_iterable2.AllRight, Is.False);
            Assert.That(severalTasksParent.Current.ToInt(), Is.EqualTo(10));
        }

        [Test]
        public void TestThatABreakAndStopBreaksTheWholeExecution()
        {
            var severalTasksParent = SeveralTasksParentBreak();
            severalTasksParent.Complete(1000); //ms

            Assert.That(_iterable1.AllRight, Is.True);
            Assert.That(_iterable2.AllRight, Is.False);
            Assert.That(severalTasksParent.Current.ToInt(), Is.Not.EqualTo(10));
        }

        [Test]
        public void BreakAndStop_DisposesEntireContinueChain_AndLeavesUnrelatedRootsRunning()
        {
            bool rootResumed = false;
            bool middleResumed = false;
            bool rootDisposed = false;
            bool middleDisposed = false;
            bool leafDisposed = false;
            bool unrelatedRootRan = false;

            IEnumerator<TaskContract> Leaf()
            {
                try
                {
                    yield return TaskContract.Break.AndStop;
                }
                finally
                {
                    leafDisposed = true;
                }
            }

            IEnumerator<TaskContract> Middle()
            {
                try
                {
                    yield return Leaf().Continue();
                    middleResumed = true;
                }
                finally
                {
                    middleDisposed = true;
                }
            }

            IEnumerator<TaskContract> Root()
            {
                try
                {
                    yield return Middle().Continue();
                    rootResumed = true;
                }
                finally
                {
                    rootDisposed = true;
                }
            }

            IEnumerator<TaskContract> UnrelatedRoot()
            {
                unrelatedRootRan = true;
                yield break;
            }

            using (var runner = new SteppableRunner("BreakAndStopChain"))
            {
                Root().RunOn(runner);
                UnrelatedRoot().RunOn(runner);

                while (runner.hasTasks)
                    runner.Step();
            }

            Assert.That(rootResumed, Is.False);
            Assert.That(middleResumed, Is.False);
            Assert.That(rootDisposed, Is.True);
            Assert.That(middleDisposed, Is.True);
            Assert.That(leafDisposed, Is.True);
            Assert.That(unrelatedRootRan, Is.True);
        }
        
        [Test]
        public void TestThatABreakItBreaksTheCurrentTaskButLetsTheCallerContinue()
        {
            //the distinguishing Break.It semantic: the broken task stops, but the task
            //that was waiting for it keeps running (unlike Break.AndStop)
            var severalTasksParent = SeveralTasksParentBreakIt();
            severalTasksParent.Complete(1000); //ms

            //the child broke itself right after _iterable1, skipping _iterable2...
            Assert.That(_iterable1.AllRight, Is.True);
            Assert.That(_iterable2.AllRight, Is.False);
            //...but the caller resumed and completed with its return value
            Assert.That(severalTasksParent.Current.ToInt(), Is.EqualTo(10));
        }

        [TearDown]
        public void TearDown()
        {
            _iterable1.Dispose();
            _iterable2.Dispose();
        }

        IEnumerator<TaskContract> SeveralTasksParent()
        {
            yield return SeveralTasks().Continue();

            yield return 10;
        }

        IEnumerator<TaskContract> SeveralTasks()
        {
            yield return _iterable1.Continue();

            yield break;

#pragma warning disable 162
            yield return _iterable2.Continue();
#pragma warning restore 162
        }

        IEnumerator<TaskContract> SeveralTasksParentBreak()
        {
            yield return SeveralTasksBreak().Continue();

            yield return 10;
        }

        IEnumerator<TaskContract> SeveralTasksBreak()
        {
            yield return _iterable1.Continue();

            yield return TaskContract.Break.AndStop;

            yield return _iterable2.Continue();
        }
        
        IEnumerator<TaskContract> SeveralTasksParentBreakIt()
        {
            yield return SeveralTasksBreakIt().Continue();

            yield return 10;
        }

        IEnumerator<TaskContract> SeveralTasksBreakIt()
        {
            yield return _iterable1.Continue();

            yield return TaskContract.Break.It;
            
            yield return _iterable2.Continue();
        }

        LeanEnumerator _iterable1;
        LeanEnumerator _iterable2;
    }
}

