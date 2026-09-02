using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class ThreadSafeCollectionsTests
    {
        [Test]
        public void ThreadSafeFasterList_GetEnumerator_ReturnsUnderlyingItems()
        {
            var list = new ThreadSafeFasterList<int>();
            list.Add(10);
            list.Add(20);

            var enumerator = list.GetEnumerator();

            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current, Is.EqualTo(10));
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current, Is.EqualTo(20));
            Assert.That(enumerator.MoveNext(), Is.False);
        }

        [Test]
        public void ThreadSafeDictionary_EnsureCapacity_ReleasesWriteLock()
        {
            using var dictionary = new ThreadSafeDictionary<int, int>();

            dictionary.EnsureCapacity(32);
            dictionary.Add(1, 10);

            Assert.That(dictionary.TryGetValue(1, out var value), Is.True);
            Assert.That(value, Is.EqualTo(10));
        }

        [Test]
        public void ThreadSafeDictionary_IncreaseCapacityBy_ReleasesWriteLock()
        {
            using var dictionary = new ThreadSafeDictionary<int, int>();

            dictionary.IncreaseCapacityBy(32);
            dictionary.Add(1, 10);

            Assert.That(dictionary.TryGetValue(1, out var value), Is.True);
            Assert.That(value, Is.EqualTo(10));
        }

        [Test]
        public void ThreadSafeFasterList_MutationAndSnapshotOperationsWork()
        {
            var list = new ThreadSafeFasterList<int>();
            list.Add(10);
            list.Add(2, 30);
            list.Insert(1, 20);

            Assert.That(list.count, Is.EqualTo(4));
            Assert.That(list[1], Is.EqualTo(20));

            list.RemoveAt(1);
            list.UnorderedRemoveAt(0);
            var snapshot = list.ToArrayFast(out var count);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(snapshot[0], Is.EqualTo(30));
            Assert.That(snapshot[1], Is.Zero);

            list.Clear();
            Assert.That(list.count, Is.Zero);
        }

        [Test]
        public void ThreadSafeFasterList_RejectsNullBackingList()
        {
            Assert.That(() => new ThreadSafeFasterList<int>(null), Throws.TypeOf<System.ArgumentException>());
        }

        [Test]
        public void ThreadSafeDictionary_PublicOperationsRoundtrip()
        {
            using var dictionary = new ThreadSafeDictionary<int, int>(1);
            dictionary.Add(1, 10);
            dictionary.Set(1, 11);
            dictionary[2] = 20;
            dictionary[2] = 22;

            Assert.That(dictionary.count, Is.EqualTo(2));
            Assert.That(dictionary.ContainsKey(1), Is.True);
            Assert.That(dictionary[1], Is.EqualTo(11));
            Assert.That(dictionary.TryFindIndex(2, out var index), Is.True);
            Assert.That(dictionary.GetIndex(2), Is.EqualTo(index));

            using (var values = dictionary.GetValues)
            {
                var buffer = values.GetValues(out var count);
                Assert.That(count, Is.EqualTo(2));
                Assert.That(buffer[0], Is.AnyOf(11, 22));
            }

            Assert.That(dictionary.TryRemove(1, out var removed), Is.True);
            Assert.That(removed, Is.EqualTo(11));
            Assert.That(dictionary.TryRemove(1), Is.False);
            Assert.That(dictionary.Remove(2), Is.True);
            dictionary.Trim();
            dictionary.Clear();

            Assert.That(dictionary.count, Is.Zero);
        }

        [Test]
        public void ThreadSafeDictionary_GetOrAddBuildsOnlyMissingValue()
        {
            using var dictionary = new ThreadSafeDictionary<int, object>();
            var calls = 0;

            var created = dictionary.GetOrAdd<string>(1, () =>
            {
                calls++;
                return "created";
            });
            var existing = dictionary.GetOrAdd<string>(1, () =>
            {
                calls++;
                return "other";
            });

            Assert.That(created, Is.EqualTo("created"));
            Assert.That(existing, Is.SameAs(created));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ThreadSafeDictionary_UnsafeRefAccessorsAreRejected()
        {
            using var dictionary = new ThreadSafeDictionary<int, int>();

            Assert.That(() => dictionary.GetDirectValueByRef(0), Throws.TypeOf<System.NotSupportedException>());
            Assert.That(() => dictionary.GetValueByRef(0), Throws.TypeOf<System.NotSupportedException>());
        }

        [Test]
        public void ThreadSafeStack_PushPopAndLockedValuesWork()
        {
            var stack = new ThreadSafeStack<int>();
            stack.Push(10);
            stack.Push(20);

            using (var values = stack.GetValues)
                Assert.That(values.GetValues(), Is.EqualTo(new[] { 20, 10 }));

            Assert.That(stack.count, Is.EqualTo(2));
            Assert.That(stack.TryPop(out var value), Is.True);
            Assert.That(value, Is.EqualTo(20));
            Assert.That(stack.TryPop(out value), Is.True);
            Assert.That(value, Is.EqualTo(10));
            Assert.That(stack.TryPop(out value), Is.False);
            Assert.That(value, Is.Zero);
        }
    }
}
