using System.Threading;
using Svelto.Tasks.Parallelism;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class ParallelJobCollectionTests
    {
        struct TestJob : ISveltoJob
        {
            public int[] results;

            public void Update(int index)
            {
                Interlocked.Increment(ref results[index]);
            }

            public void Dispose() { }
        }

        [Test]
        public void MultiThreadedParallelJobCollection_RunsJobsInParallel()
        {
            // What we are testing:
            // MultiThreadedParallelJobCollection distributes job iterations across multiple threads.

            var job = new TestJob { results = new int[1024] };
            using (var collection = new MultiThreadedParallelJobCollection<TestJob>("test", 4, false))
            {
                collection.Add(job, 1024);

                // Job collection is an IEnumerator<TaskContract>, it can be run like any other task
                // We use Complete() extension to run it synchronously for the test
                collection.Complete(2000);
            }

            for (int i = 0; i < 1024; i++)
                Assert.That(job.results[i], Is.EqualTo(1), $"Index {i} was not updated correctly");
        }

        sealed class JobCounter
        {
            public int disposeCounter;
        }

        struct DisposableJob : ISveltoJob
        {
            public JobCounter counter;

            public void Update(int index)
            {
            }

            public void Dispose()
            {
                Interlocked.Increment(ref counter.disposeCounter);
            }
        }

        [Test]
        public void MultiThreadedParallelJobCollection_Dispose_DisposesJobs()
        {
            var counter = new JobCounter();
            var job = new DisposableJob { counter = counter };

            var collection = new MultiThreadedParallelJobCollection<DisposableJob>("dispose-test", 4, false);
            collection.Add(job, 1024);

            collection.Dispose();

            Assert.That(counter.disposeCounter, Is.EqualTo(4));
        }
    }
}
