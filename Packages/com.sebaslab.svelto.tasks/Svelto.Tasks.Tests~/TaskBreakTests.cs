using System.Collections.Generic;

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

