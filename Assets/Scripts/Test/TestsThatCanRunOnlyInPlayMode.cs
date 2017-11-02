using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System;
using System.Diagnostics;
using NUnit.Framework.Constraints;
using Svelto.Tasks;

public class TestsThatCanRunOnlyInPlayMode
{
    [SetUp]
    public void Setup()
    {
        iterable1 = new Enumerable(10000);
    }
    // A UnityTest behaves like a coroutine in PlayMode
    // and allows you to yield null to skip a frame in EditMode
    [UnityTest]
	public IEnumerator TestHandlingInstructionsToUnity() {
        // Use the Assert class to test conditions.
        // yield to skip a frame
        var task = UnityHandle().Run();

        while (task.MoveNext())
            yield return null;
    }

    IEnumerator UnityHandle()
    {
        DateTime now = DateTime.Now;

        yield return new UnityEngine.WaitForSeconds(2);

        var seconds = (DateTime.Now - now).Seconds;

        Assert.That(seconds == 2);
    }

    [UnityTest]
    public IEnumerator TestMultithreadIntervaled()
    {
        using (var runner = new MultiThreadRunner("intervalTest", 1))
        {
            DateTime now = DateTime.Now;

            var task = iterable1.GetEnumerator().ThreadSafeRunOnSchedule(runner);

            while (task.MoveNext())
                yield return null;

            var seconds = (DateTime.Now - now).Seconds;

            //10000 iteration * 1ms = 10 seconds

            Assert.That(iterable1.AllRight == true && seconds == 10);
        }
    }

    Enumerable iterable1;

    class Enumerable : IEnumerable
    {
        public long endOfExecutionTime { get; private set; }

        public bool AllRight
        {
            get
            {
                return iterations == totalIterations;
            }
        }

        public Enumerable(int niterations)
        {
            iterations = 0;
            totalIterations = niterations;
        }

        public void Reset()
        {
            iterations = 0;
        }

        public IEnumerator GetEnumerator()
        {
            Reset();

            if (totalIterations < 0)
                throw new Exception("can't handle this");

            while (iterations < totalIterations)
            {
                iterations++;

                yield return null;
            }

            endOfExecutionTime = DateTime.Now.Ticks;
        }

        int totalIterations;
        int iterations;
    }
}
