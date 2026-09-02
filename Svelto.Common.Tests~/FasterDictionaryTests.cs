using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public partial class FasterDictionaryTests
    {
        [Test]
        public void Add_DuplicateKey_Throws()
        {
            var dictionary = new FasterDictionary<int, string>();
            dictionary.Add(1, "one");

#if DEBUG
            Assert.That(() => dictionary.Add(1, "another one"), Throws.TypeOf<SveltoDictionaryException>());
#else
            dictionary.Add(1, "another one");
            Assert.That(dictionary[1], Is.EqualTo("another one"));
#endif
        }

        [Test]
        public void Intersect_RetainsOnlySharedKeys()
        {
            var first = new FasterDictionary<int, int>();
            var second = new FasterDictionary<int, int>();

            for (var i = 0; i < 100; i++)
                first.Add(i, i);

            for (var i = 0; i < 200; i += 2)
                second.Add(i, i);

            first.Intersect(second);

            Assert.That(first.count, Is.EqualTo(50));
            for (var i = 0; i < 100; i += 2)
                Assert.That(first.ContainsKey(i), Is.True);
        }

        [Test]
        public void Exclude_RemovesSharedKeys()
        {
            var first = new FasterDictionary<int, int>();
            var second = new FasterDictionary<int, int>();

            for (var i = 0; i < 100; i++)
                first.Add(i, i);

            for (var i = 0; i < 200; i += 2)
                second.Add(i, i);

            first.Exclude(second);

            Assert.That(first.count, Is.EqualTo(50));
            for (var i = 1; i < 100; i += 2)
                Assert.That(first.ContainsKey(i), Is.True);
        }

        [Test]
        public void Union_AddsMissingKeys()
        {
            var first = new FasterDictionary<int, int>();
            var second = new FasterDictionary<int, int>();

            for (var i = 0; i < 100; i++)
                first.Add(i, i);

            for (var i = 0; i < 200; i += 2)
                second.Add(i, i);

            first.Union(second);

            Assert.That(first.count, Is.EqualTo(150));
            for (var i = 0; i < 200; i++)
                Assert.That(first.ContainsKey(i), Is.EqualTo(i < 100 || i % 2 == 0));
        }

        [Test]
        public void Assignment_Remove_Trim_AndReinsert_PreserveValues()
        {
            const int dictionarySize = 1000;
            var dictionary = new FasterDictionary<int, int>();
            var keys = new int[dictionarySize];

            for (var i = 1; i < dictionarySize; i++)
                keys[i] = keys[i - 1] + i * HashHelpers.Expand(dictionarySize);

            for (var i = 0; i < dictionarySize; i++)
                dictionary[keys[i]] = i;

            for (var i = 0; i < dictionarySize; i += 2)
                Assert.That(dictionary.Remove(keys[i]), Is.True);

            dictionary.Trim();

            for (var i = 0; i < dictionarySize; i++)
                dictionary[keys[i]] = i;

            for (var i = 0; i < dictionarySize; i++)
                Assert.That(dictionary[keys[i]], Is.EqualTo(i));

            dictionary.Clear();

            Assert.That(dictionary.count, Is.EqualTo(0));
            Assert.That(dictionary.ContainsKey(keys[0]), Is.False);
        }
    }
}
