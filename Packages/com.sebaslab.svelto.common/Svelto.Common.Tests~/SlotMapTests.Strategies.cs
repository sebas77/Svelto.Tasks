using Svelto.DataStructures;
using Svelto.DataStructures.Native;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace Svelto.Common.Tests
{
    public partial class SlotMapTests
    {
        class Test
        {
            uint _id;

            public Test(uint id)
            {
                _id = id;
            }
        }

        [Test]
        public void TestResize()
        {
            uint initialCount = 5;

            SlotMap<Test, ManagedStrategy<Test>, NativeStrategy<SparseIndex>> testContainer = new(initialCount);

            for (uint i = 0; i < initialCount + 1; i++)
            {
                Assert.DoesNotThrow(() => testContainer.Add(new(i)));
                Assert.IsTrue(testContainer.capacity >= initialCount);
            }

            testContainer.Dispose();
        }

        [Test]
        public void TestRemoveSequence()
        {
            SlotMap<Test, ManagedStrategy<Test>, NativeStrategy<SparseIndex>> testContainer = new(10);

            ValueIndex[] indexArray = new ValueIndex[10];

            for (uint i = 0; i < 10; i++)
            {
                indexArray[i] = testContainer.Add(new(i));
            }

            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[9]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[8]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[7]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[6]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[5]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[4]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[3]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[2]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[1]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[0]));

            testContainer.Dispose();
        }

        [Test]
        public void TestRemoveUnsequenced()
        {
            SlotMap<Test, ManagedStrategy<Test>, NativeStrategy<SparseIndex>> testContainer = new(10);

            ValueIndex[] indexArray = new ValueIndex[10];

            for (uint i = 0; i < 10; i++)
            {
                indexArray[i] = testContainer.Add(new(i));
            }

            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[9]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[7]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[6]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[4]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[1]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[8]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[5]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[3]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[0]));
            Assert.DoesNotThrow(() => testContainer.Remove(indexArray[2]));

            testContainer.Dispose();
        }

        [Test]
        public void TestGetAfterRemoveUnsequenced()
        {
            SlotMap<Test, ManagedStrategy<Test>, NativeStrategy<SparseIndex>> testContainer = new(10);

            ValueIndex[] indexArray = new ValueIndex[10];
            Test[] testArray = new Test[10];

            for (uint i = 0; i < 10; i++)
            {
                testArray[i] = new(i);
                indexArray[i] = testContainer.Add(testArray[i]);
            }

            testContainer.Remove(indexArray[7]);
            Assert.AreEqual(testArray[9], testContainer[indexArray[9]]);

            testContainer.Dispose();
        }

        [Test]
        public void TestGetAfterRemoveSequenced()
        {
            SlotMap<Test, ManagedStrategy<Test>, NativeStrategy<SparseIndex>> testContainer = new(10);

            ValueIndex[] indexArray = new ValueIndex[10];
            Test[] testArray = new Test[10];

            for (uint i = 0; i < 10; i++)
            {
                testArray[i] = new(i);
                indexArray[i] = testContainer.Add(testArray[i]);
            }

            testContainer.Remove(indexArray[9]);
            Assert.AreEqual(testArray[7], testContainer[indexArray[7]]);

            testContainer.Dispose();
        }

        [Test]
        public void RandomAddAndRemoveTest()
        {
            SlotMap<Test, ManagedStrategy<Test>, NativeStrategy<SparseIndex>> test =
                    new SlotMap<Test, ManagedStrategy<Test>, NativeStrategy<SparseIndex>>(16);

            var index = test.Add(new Test(0));
            var b = test.Has(index);
            Assert.IsTrue(b);
            index = test.Add(new Test(1));
            b = test.Has(index);
            Assert.IsTrue(b);
            var index2 = test.Add(new Test(2));
            b = test.Has(index2);
            Assert.IsTrue(b);
            test.Remove(index2);
            b = test.Has(index2);
            Assert.IsFalse(b);
            index = test.Add(new Test(3));
            b = test.Has(index);
            Assert.IsTrue(b);
            b = test.Has(index2);
            Assert.IsFalse(b);
            index = test.Add(new Test(2));
            b = test.Has(index);
            Assert.IsTrue(b);
            b = test.Has(index2);
            Assert.IsFalse(b);

            test.Dispose();
        }
        
        
        [Test]
        public void TestFreeList()
        {
            SlotMap<Test, ManagedStrategy<Test>, NativeStrategy<SparseIndex>> test =
                    new SlotMap<Test, ManagedStrategy<Test>, NativeStrategy<SparseIndex>>(16);

            var indexA = test.Add(new Test(0));
            test.Add(new Test(2));
            var indexB = test.Add(new Test(3));
            test.Add(new Test(1));
            
            test.Remove(indexA);
            test.Remove(indexB);

            var capacity = test.capacity;

            var indexC = test.Add(new Test(0));
            var indexD = test.Add(new Test(3));
            
            Assert.That(test.count, Is.EqualTo(4));
            Assert.That(test.capacity, Is.EqualTo(capacity));
            Assert.That(test.Has(indexC));
            Assert.That(test.Has(indexD));
            Assert.That(test.Has(indexA), Is.EqualTo(false));
            Assert.That(test.Has(indexB), Is.EqualTo(false));

            test.Dispose();
        }
    }
}
