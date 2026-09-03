using Svelto.DataStructures;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace Svelto.Common.Tests
{
    public partial class FasterDictionaryTests
    {
        [TestCase(Description = "Test adding an existing key throws")]
        public void TestUniqueKey()
        {
            FasterDictionary<int, string> test = new FasterDictionary<int, string>();

            test.Add(1, "one.a");
            void TestAddDup() { test.Add(1, "one.b"); }

#if DEBUG
            Assert.Throws(typeof(SveltoDictionaryException), TestAddDup);
#else
            TestAddDup();
            Assert.AreEqual("one.b", test[1]);
#endif
        }

        [TestCase]
        public void TestIntersect()
        {
            FasterDictionary<int, int> test1 = new FasterDictionary<int, int>();
            FasterDictionary<int, int> test2 = new FasterDictionary<int, int>();

            for (int i = 0; i < 100; ++i)
                test1.Add(i, i);

            for (int i = 0; i < 200; i += 2)
                test2.Add(i, i);

            test1.Intersect(test2);

            Assert.AreEqual(50, test1.count);

            for (int i = 0; i < 100; i += 2)
                Assert.IsTrue(test1.ContainsKey(i));
        }
        
        [TestCase]
        public void TestIntersect2()
        {
            FasterDictionary<int, int> test1 = new FasterDictionary<int, int>();
            FasterDictionary<int, int> test2 = new FasterDictionary<int, int>();

            test1.Add(1, 1);
            test1.Add(2, 2);
            test1.Add(3, 3);
            test1.Add(4, 4);

            test2.Add(1, 1);
            test2.Add(2, 2);
            test2.Add(5, 5);
            test2.Add(4, 4);

            test1.Intersect(test2);
            
            Assert.AreEqual(3, test1.count);

            Assert.That(test1[1], Is.EqualTo(1));
            Assert.That(test1[2], Is.EqualTo(2));
            Assert.That(test1[4], Is.EqualTo(4));
            
            test2.Intersect(test1);
            
            Assert.AreEqual(3, test2.count);

            Assert.That(test2[1], Is.EqualTo(1));
            Assert.That(test2[2], Is.EqualTo(2));
            Assert.That(test2[4], Is.EqualTo(4));
        }
        
        [TestCase]
        public void TestIntersect3()
        {
            FasterDictionary<int, int> test1 = new FasterDictionary<int, int>();
            FasterDictionary<int, int> test2 = new FasterDictionary<int, int>();

            test1.Add(1, 1);
            test1.Add(2, 2);
            test1.Add(3, 3);
            test1.Add(4, 4);

            test2.Add(1, 1);
            test2.Add(6, 6);
            test2.Add(5, 5);
            test2.Add(4, 4);

            test1.Intersect(test2);
            
            Assert.AreEqual(2, test1.count);

            Assert.That(test1[1], Is.EqualTo(1));
            Assert.That(test1[4], Is.EqualTo(4));
            
            test2.Intersect(test1);
            
            Assert.AreEqual(2, test2.count);

            Assert.That(test2[1], Is.EqualTo(1));
            Assert.That(test2[4], Is.EqualTo(4));
        }

        [TestCase]
        public void TestExclude()
        {
            FasterDictionary<int, int> test1 = new FasterDictionary<int, int>();
            FasterDictionary<int, int> test2 = new FasterDictionary<int, int>();

            for (int i = 0; i < 100; ++i)
                test1.Add(i, i);

            for (int i = 0; i < 200; i += 2)
                test2.Add(i, i);

            test1.Exclude(test2);

            Assert.AreEqual(50, test1.count);

            for (int i = 1; i < 100; i += 2)
                Assert.IsTrue(test1.ContainsKey(i));
        }

        [TestCase]
        public void TestUnion()
        {
            FasterDictionary<int, int> test1 = new FasterDictionary<int, int>();
            FasterDictionary<int, int> test2 = new FasterDictionary<int, int>();

            for (int i = 0; i < 100; ++i)
                test1.Add(i, i);

            for (int i = 0; i < 200; i += 2)
                test2.Add(i, i);

            test1.Union(test2);

            Assert.AreEqual(150, test1.count);

            for (int i = 0; i < 100; i++)
                Assert.IsTrue(test1.ContainsKey(i));

            for (int i = 100; i < 200; i += 2)
                Assert.IsTrue(test1.ContainsKey(i));
        }

        [TestCase]
        public void TestFastClear()
        {
            FasterDictionary<int, int> test = new FasterDictionary<int, int>();

            test.Add(0, 0);

            Assert.IsTrue(test.ContainsKey(0));

            test.Clear();

            Assert.IsFalse(test.ContainsKey(0));
        }
    }
}
