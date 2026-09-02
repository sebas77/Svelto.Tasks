using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class ConcurrencyTests
    {
        [Test]
        public void ThreadSafeFasterList_ConcurrentAddsPreserveEveryItem()
        {
            var list = new ThreadSafeFasterList<int>();
            using var gate = new ManualResetEventSlim(false);
            var tasks = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
            {
                gate.Wait();
                for (var item = 0; item < 100; item++)
                    list.Add(worker * 100 + item);
            })).ToArray();

            gate.Set();
            Assert.That(Task.WaitAll(tasks, 5000), Is.True);

            var values = list.ToArrayFast(out var count).Take(count).ToArray();
            Assert.That(count, Is.EqualTo(800));
            Assert.That(values, Is.EquivalentTo(Enumerable.Range(0, 800)));
        }

        [Test]
        public void ThreadSafeDictionary_ConcurrentGetOrAddBuildsValueOnce()
        {
            using var dictionary = new ThreadSafeDictionary<int, object>();
            using var gate = new ManualResetEventSlim(false);
            var builderCalls = 0;
            var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                gate.Wait();
                return dictionary.GetOrAdd<string>(1, () =>
                {
                    Interlocked.Increment(ref builderCalls);
                    return "value";
                });
            })).ToArray();

            gate.Set();
            Assert.That(Task.WaitAll(tasks, 5000), Is.True);

            Assert.That(builderCalls, Is.EqualTo(1));
            Assert.That(dictionary.count, Is.EqualTo(1));
            Assert.That(tasks.Select(task => task.Result), Is.All.EqualTo("value"));
        }

        [Test]
        public void ThreadSafeStack_ConcurrentPushAndPopLoseNoValues()
        {
            var stack = new ThreadSafeStack<int>();
            var pushTasks = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
            {
                for (var item = 0; item < 100; item++)
                    stack.Push(worker * 100 + item);
            })).ToArray();

            Assert.That(Task.WaitAll(pushTasks, 5000), Is.True);

            var popped = new System.Collections.Concurrent.ConcurrentBag<int>();
            var popTasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                while (stack.TryPop(out var value))
                    popped.Add(value);
            })).ToArray();

            Assert.That(Task.WaitAll(popTasks, 5000), Is.True);
            Assert.That(stack.count, Is.Zero);
            Assert.That(popped, Is.EquivalentTo(Enumerable.Range(0, 800)));
        }

        [Test]
        public void ReaderWriterLockSlimEx_WriteWaitsForActiveReader()
        {
            var rwLock = ReaderWriterLockSlimEx.Create();
            using var writerStarted = new ManualResetEventSlim(false);
            using var writerAcquired = new ManualResetEventSlim(false);
            rwLock.EnterReadLock();

            var writer = Task.Run(() =>
            {
                writerStarted.Set();
                rwLock.EnterWriteLock();
                try
                {
                    writerAcquired.Set();
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
            });

            Assert.That(writerStarted.Wait(5000), Is.True);
            Assert.That(writerAcquired.Wait(50), Is.False);

            rwLock.ExitReadLock();

            Assert.That(writerAcquired.Wait(5000), Is.True);
            Assert.That(writer.Wait(5000), Is.True);
        }

    }
}
