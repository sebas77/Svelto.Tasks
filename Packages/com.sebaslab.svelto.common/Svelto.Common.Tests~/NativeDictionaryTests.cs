using Svelto.DataStructures;
using Svelto.DataStructures.Native;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class NativeDictionaryTests
    {
        [Test]
        public void SveltoDictionaryNative_PublicOperationsRoundtrip()
        {
            var dictionary = new SveltoDictionaryNative<int, int>(1);
            try
            {
                dictionary.Add(1, 10);
                dictionary[2] = 20;
                dictionary.Set(1, 11);
                ref var defaultValue = ref dictionary.GetOrAdd(3);
                defaultValue = 30;
                ref var builtValue = ref dictionary.GetOrAdd(4, () => 40);
                builtValue++;

                Assert.That(dictionary.count, Is.EqualTo(4));
                Assert.That(dictionary.ContainsKey(1), Is.True);
                Assert.That(dictionary.TryGetValue(2, out var value), Is.True);
                Assert.That(value, Is.EqualTo(20));
                Assert.That(dictionary.TryFindIndex(3, out var index), Is.True);
                Assert.That(dictionary.GetIndex(3), Is.EqualTo(index));

                dictionary.GetValueByRef(1) = 12;
                dictionary.GetDirectValueByRef(index) = 31;
                var values = dictionary.GetValues(out var count);

                Assert.That(count, Is.EqualTo(4));
                Assert.That(values[(int)dictionary.GetIndex(1)], Is.EqualTo(12));
                Assert.That(dictionary[3], Is.EqualTo(31));
                Assert.That(dictionary[4], Is.EqualTo(41));

                dictionary.EnsureCapacity(16);
                dictionary.IncreaseCapacityBy(4);
                Assert.That(dictionary.Remove(2), Is.True);
                Assert.That(dictionary.Remove(2), Is.False);
                dictionary.Trim();
                dictionary.Clear();
                Assert.That(dictionary.count, Is.Zero);
            }
            finally
            {
                dictionary.Dispose();
            }
        }

        [Test]
        public void SharedSveltoDictionary_CopiesAndReadonlyViewObserveSameStorage()
        {
            var dictionary = new SharedSveltoDictionaryNative<int, int>(1);
            try
            {
                dictionary.Add(1, 10);
                var alias = dictionary;
                alias.Set(1, 20);
                Assert.That(alias.TryAdd(2, 30, out var index), Is.True);
                Assert.That(alias.TryAdd(2, 99, out _), Is.False);

                ReadonlySharedSveltoDictionaryNative<int, int> readOnly = dictionary;

                Assert.That(dictionary.isValid, Is.True);
                Assert.That(dictionary.isDisposed, Is.False);
                Assert.That(dictionary[1], Is.EqualTo(20));
                Assert.That(dictionary.GetDirectValueByRef(index), Is.EqualTo(30));
                Assert.That(readOnly.isValid, Is.True);
                Assert.That(readOnly.count, Is.EqualTo(2));
                Assert.That(readOnly.ContainsKey(2), Is.True);
                Assert.That(readOnly.TryGetValue(2, out var value), Is.True);
                Assert.That(value, Is.EqualTo(30));
                Assert.That(readOnly.GetIndex(2), Is.EqualTo(index));
                Assert.That(readOnly.TryFindIndex(1, out _), Is.True);
                Assert.That(readOnly[1], Is.EqualTo(20));

                var values = readOnly.GetValues(out var count);
                Assert.That(count, Is.EqualTo(2));
                Assert.That(values[(int)index], Is.EqualTo(30));
            }
            finally
            {
                dictionary.Dispose();
            }

            Assert.That(dictionary.isDisposed, Is.True);
        }

        [Test]
        public void SharedSveltoDictionary_EnumeratorExposesKeysAndMutableValues()
        {
            var dictionary = SharedSveltoDictionaryNative<int, int>.Create();
            try
            {
                dictionary.Add(1, 10);
                dictionary.Add(2, 20);
                var keys = new System.Collections.Generic.HashSet<int>();

                foreach (var pair in dictionary)
                {
                    keys.Add(pair.key);
                    pair.value++;
                }

                Assert.That(keys, Is.EquivalentTo(new[] { 1, 2 }));
                Assert.That(dictionary[1], Is.EqualTo(11));
                Assert.That(dictionary[2], Is.EqualTo(21));
                Assert.That(dictionary.unsafeValues[(int)dictionary.GetIndex(1)], Is.EqualTo(11));
            }
            finally
            {
                dictionary.Dispose();
            }
        }

        [Test]
        public void SharedSveltoDictionary_EnumeratorRejectsStructuralMutation()
        {
            var dictionary = SharedSveltoDictionaryNative<int, int>.Create();
            try
            {
                dictionary.Add(1, 10);
                var enumerator = dictionary.GetEnumerator();
                dictionary.Add(2, 20);

#if DEBUG
                Assert.That(() => enumerator.MoveNext(), Throws.TypeOf<SveltoDictionaryException>());
#else
                Assert.That(() => enumerator.MoveNext(), Throws.Nothing);
#endif
            }
            finally
            {
                dictionary.Dispose();
            }
        }

        [Test]
        public void SharedSveltoDictionary_EnumeratorAllowsValueAndFailedStructuralUpdates()
        {
            var dictionary = SharedSveltoDictionaryNative<int, int>.Create();
            try
            {
                dictionary.Add(1, 10);
                var enumerator = dictionary.GetEnumerator();

                dictionary.Set(1, 20);
                Assert.That(dictionary.TryAdd(1, 30, out _), Is.False);
                Assert.That(dictionary.Remove(999), Is.False);

                Assert.That(enumerator.MoveNext(), Is.True);
                Assert.That(enumerator.Current.key, Is.EqualTo(1));
                Assert.That(enumerator.Current.value, Is.EqualTo(20));
            }
            finally
            {
                dictionary.Dispose();
            }
        }

        [Test]
        public void SharedSveltoDictionary_EnumeratorRejectsCapacityReallocation()
        {
            var dictionary = SharedSveltoDictionaryNative<int, int>.Create();
            try
            {
                dictionary.Add(1, 10);
                var enumerator = dictionary.GetEnumerator();

                dictionary.EnsureCapacity(100);

#if DEBUG
                Assert.That(() => enumerator.MoveNext(), Throws.TypeOf<SveltoDictionaryException>());
#else
                Assert.That(() => enumerator.MoveNext(), Throws.Nothing);
#endif
            }
            finally
            {
                dictionary.Dispose();
            }
        }

        [Test]
        public void SveltoDictionary_KeyEnumeratorRejectsStructuralMutationAcrossCopies()
        {
            var dictionary = new SveltoDictionary<int, int, NativeStrategy<SveltoDictionaryNode<int>>,
                NativeStrategy<int>, NativeStrategy<int>>(1, Allocator.Persistent);
            try
            {
                dictionary.Add(1, 10);
                var enumerator = dictionary.keys.GetEnumerator();

                dictionary.Add(2, 20);

#if DEBUG
                Assert.That(() => enumerator.MoveNext(), Throws.TypeOf<SveltoDictionaryException>());
#else
                Assert.That(() => enumerator.MoveNext(), Throws.Nothing);
#endif
            }
            finally
            {
                dictionary.Dispose();
            }
        }

        [Test]
        public void LocalNativeDictionaryViewsRoundtripRawDictionaryState()
        {
            var raw = new SveltoDictionary<int, int, NativeStrategy<SveltoDictionaryNode<int>>, NativeStrategy<int>,
                NativeStrategy<int>>(1, Allocator.Persistent);
            LocalSveltoDictionaryNative<int, int> local = raw;
            local.Add(1, 10);
            local.GetOrAdd(2, () => 20)++;
            local.EnsureCapacity(8);
            local.IncreaseCapacityBy(2);
            raw = local;

            LocalReadonlySveltoDictionaryNative<int, int> readOnly = raw;
            try
            {
                Assert.That(local.count, Is.EqualTo(2));
                Assert.That(local.ContainsKey(1), Is.True);
                Assert.That(local.TryGetValue(2, out var value), Is.True);
                Assert.That(value, Is.EqualTo(21));
                Assert.That(readOnly.count, Is.EqualTo(2));
                Assert.That(readOnly[1], Is.EqualTo(10));
                Assert.That(readOnly.TryFindIndex(2, out var index), Is.True);
                Assert.That(readOnly.GetIndex(2), Is.EqualTo(index));
                Assert.That(readOnly.GetValues(out var count)[(int)index], Is.EqualTo(21));
                Assert.That(count, Is.EqualTo(2));

                local.GetDirectValueByRef(index) = 22;
                Assert.That(local.Remove(1), Is.True);
                local.Trim();
                Assert.That(local[2], Is.EqualTo(22));
                local.Clear();
                Assert.That(local.count, Is.Zero);
            }
            finally
            {
                local.Dispose();
            }
        }

        [Test]
        public void OwningReadonlyNativeDictionary_RepresentsEmptyAllocatedDictionary()
        {
            var dictionary = new ReadonlySveltoDictionaryNative<int, int>(4);
            try
            {
                Assert.That(dictionary.count, Is.Zero);
                Assert.That(dictionary.ContainsKey(1), Is.False);
                Assert.That(dictionary.TryGetValue(1, out var value), Is.False);
                Assert.That(value, Is.Zero);
                Assert.That(dictionary.TryFindIndex(1, out _), Is.False);
                Assert.That(dictionary.GetValues(out var count).capacity, Is.GreaterThanOrEqualTo(4));
                Assert.That(count, Is.Zero);
            }
            finally
            {
                dictionary.Dispose();
            }
        }
    }
}
