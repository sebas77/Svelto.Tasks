using Svelto.DataStructures;
using Assert = NUnit.Framework.Assert;

namespace Svelto.Common.Tests
{
    public partial class FasterListTests
    {
        [TestCase]
        public void TestAllocationSize0()
        {
            FasterList<int> list = new FasterList<int>();

            Assert.That(list.capacity, Is.EqualTo(0));
            Assert.That(list.count, Is.EqualTo(0));
        }
        
        [TestCase]
        public void TestAllocationSize1()
        {
            FasterList<int> list = new FasterList<int>(1);

            Assert.That(list.capacity, Is.EqualTo(1));
            Assert.That(list.count, Is.EqualTo(0));
        }
        
        [TestCase]
        public void TestResize()
        {
            FasterList<int> list = new FasterList<int>(0);
            
            list.Resize(10);

            Assert.That(list.capacity, Is.EqualTo(10));
            Assert.That(list.count, Is.EqualTo(0));
        }
        
        [TestCase]
        public void TestExpandTo()
        {
            FasterList<int> list = new FasterList<int>(0);
            
            list.SetCountTo(10);

            Assert.That(list.capacity, Is.EqualTo(15));
            Assert.That(list.count, Is.EqualTo(10));
        }
        
        [TestCase]
        public void TestExpandByFromZero()
        {
            FasterList<int> list = new FasterList<int>(0);
            
            list.SetCountTo(10);

            Assert.That(list.capacity, Is.EqualTo(15));
            Assert.That(list.count, Is.EqualTo(10));
        }
        
        [TestCase]
        public void TestSet()
        {
            FasterList<int> list = new FasterList<int>(0);
            
            list.IncrementCountBy(10);
            
            for (int i = 0; i < 10; i++)
                list[i] = i;
            
            for (int i = 0; i < 10; i++)
                Assert.That(list[i], Is.EqualTo(i));

        }

        [TestCase]
        public void TestAdd()
        {
            FasterList<int> list = new FasterList<int>();

            for (int i = 0; i < 10; i++)
                list.Add(i);
            
            for (int i = 0; i < 10; i++)
                Assert.That(list[i], Is.EqualTo(i));

            Assert.That(list.capacity, Is.EqualTo(10));
            Assert.That(list.count, Is.EqualTo(10));
        }
        
        [TestCase]
        public void TestExpandBy()
        {
            FasterList<int> list = new FasterList<int>(3);
            
            list.Add(0);
            list.Add(1);
            list.Add(2);
            
            list.IncrementCountBy(10);

            Assert.That(list.capacity, Is.EqualTo(19));
            Assert.That(list.count, Is.EqualTo(13));
        }

        [TestCase]
        public void TestAddRange()
        {
            FasterList<int> listA = new FasterList<int>();
            FasterList<int> listB = new FasterList<int>();

            for (int i = 0; i < 10; i++)
                listA.Add(i);
            
            for (int i = 0; i < 11; i++)
                listB.Add(i);

            listA.AddRange(listB);
            
            for (int i = 0; i < 10; i++)
                Assert.That(listA[i], Is.EqualTo(i));
            
            for (int i = 10; i < 21; i++)
                Assert.That(listA[i], Is.EqualTo(i-10));
        }
        
        [TestCase]
        public void TestEnsureCapacity()
        {
            FasterList<int> listA = new FasterList<int>();
            
            listA.Resize(10);
            
            Assert.That(listA.capacity, Is.EqualTo(10));
            Assert.That(listA.count, Is.EqualTo(0));
        }
        
        [TestCase]
        public void TestEnsureExtraCapacity()
        {
            FasterList<int> listA = new FasterList<int>();
            
            listA.IncrementCountBy(10);
            listA.IncreaseCapacityBy(10);
            
            Assert.That(listA.capacity, Is.EqualTo(25));
            Assert.That(listA.count, Is.EqualTo(10));
        }
        
        [TestCase]
        public void TestPush()
        {
            FasterList<int> list = new FasterList<int>();

            for (int i = 0; i < 10; i++)
                list.Push(i);
            
            for (int i = 0; i < 10; i++)
                Assert.That(list[i], Is.EqualTo(i));

            Assert.That(list.capacity, Is.EqualTo(10));
            Assert.That(list.count, Is.EqualTo(10));
        }
        [TestCase]
        public void TestPop()
        {
            FasterList<int> list = new FasterList<int>();

            for (int i = 0; i < 10; i++)
                list.Push(i);
            
            for (int i = 9; i >= 0; i--)
                Assert.That(list.Pop(), Is.EqualTo(i));

            Assert.That(list.capacity, Is.EqualTo(10));
            Assert.That(list.count, Is.EqualTo(0));
        }
        [TestCase]
        public void TestPeek()
        {
            FasterList<int> list = new FasterList<int>();

            for (int i = 0; i < 10; i++)
                list.Push(i);
            
            Assert.That(list.Peek(), Is.EqualTo(9));

            Assert.That(list.capacity, Is.EqualTo(10));
            Assert.That(list.count, Is.EqualTo(10));
        }
        
        [TestCase]
        public void TestInsert()
        {
            FasterList<int> list = new FasterList<int>();

            for (int i = 0; i < 10; i++)
                list.InsertAt(0, i);
            
            for (int i = 0; i < 10; i++)
                Assert.That(list.Pop(), Is.EqualTo(i));

            Assert.That(list.capacity, Is.EqualTo(10));
            Assert.That(list.count, Is.EqualTo(0));
        }
        
        [TestCase]
        public void TestRemoveAt()
        {
            FasterList<int> list = new FasterList<int>();

            for (int i = 0; i < 10; i++)
                list.Add(i);
            
            list.RemoveAt(3);
            Assert.That(list[3], Is.EqualTo(4));
            
            list.RemoveAt(0);
            Assert.That(list[0], Is.EqualTo(1));
            
            list.RemoveAt(7);
            
            Assert.That(list.capacity, Is.EqualTo(10));
            Assert.That(list.count, Is.EqualTo(7));
        }
        
        [TestCase]
        public void TestUnorderedRemoveAt()
        {
            FasterList<int> list = new FasterList<int>();

            for (int i = 0; i < 10; i++)
                list.Add(i);
            
            list.UnorderedRemoveAt(3);
            Assert.That(list[3], Is.EqualTo(9));
            
            list.UnorderedRemoveAt(0);
            Assert.That(list[0], Is.EqualTo(8));
            
            list.UnorderedRemoveAt(7);
            
            Assert.That(list.capacity, Is.EqualTo(10));
            Assert.That(list.count, Is.EqualTo(7));
        }
        
        [TestCase]
        public void TestTrim()
        {
            FasterList<int> list = new FasterList<int>();

            for (int i = 0; i < 10; i++)
                list.Add(i);
            
            list.UnorderedRemoveAt(3);
            list.UnorderedRemoveAt(0);
            list.UnorderedRemoveAt(7);
            
            list.Trim();
            
            Assert.That(list.capacity, Is.EqualTo(7));
            Assert.That(list.count, Is.EqualTo(7));
        }
        
        [TestCase]
        public void TesSetAt()
        {
            FasterList<int> list = new FasterList<int>();
            
            list.AddAt(10, 10);
            
            Assert.That(list[10], Is.EqualTo(10));
            
            Assert.That(list.capacity, Is.EqualTo(16));
            Assert.That(list.count, Is.EqualTo(11));
        }
        
        [TestCase]
        public void TesSetAt0()
        {
            FasterList<int> list = new FasterList<int>();
            
            list.AddAt(0, 10);
            
            Assert.That(list[0], Is.EqualTo(10));
            
            Assert.That(list.capacity, Is.EqualTo(1));
            Assert.That(list.count, Is.EqualTo(1));
        }
        
        [TestCase]
        public void TesSetAtCapacity()
        {
            FasterList<int> list = new FasterList<int>(3);
            
            list.AddAt(3, 10);
            
            Assert.That(list[3], Is.EqualTo(10));
            
            Assert.That(list.capacity, Is.EqualTo(6));
            Assert.That(list.count, Is.EqualTo(4));
        }
        
        [TestCase]
        public void TestContains()
        {
            FasterList<int> list = new FasterList<int>();
            list.Add(10);

            Assert.That(list.Contains(10));
            Assert.That(list.Contains(20), Is.EqualTo(false));
        }
        [TestCase]
        public void TestFastClear()
        {
            FasterList<int> list = new FasterList<int>();
            list.Add(10);
            list.Add(20);

            list.MemClear();

            Assert.That(list.count, Is.EqualTo(2));
            Assert.That(list[0], Is.EqualTo(0));
            Assert.That(list[1], Is.EqualTo(0));
        }
        [TestCase]
        public void TestResetToReuse()
        {
            FasterList<int> list = new FasterList<int>();
            list.Add(10);
            var capacity = list.capacity;

            list.ResetToReuse();

            Assert.That(list.count, Is.EqualTo(0));
            Assert.That(list.capacity, Is.EqualTo(capacity));
        }
        
        [TestCase]
        public void TestReuseOneSlot()
        {
            FasterList<int> list = new FasterList<int>();
            list.Add(10);
            list.ResetToReuse();

            Assert.That(list.ReuseOneSlot());
            Assert.That(list.count, Is.EqualTo(1));
            Assert.That(list[0], Is.EqualTo(10));
        }
        [TestCase]
        public void TestCopyTo()
        {
            FasterList<int> list = new FasterList<int>();
            list.Add(10);
            list.Add(20);
            var destination = new int[4];

            list.CopyTo(destination, 1);

            Assert.That(destination, Is.EqualTo(new[] { 0, 10, 20, 0 }));
        }
        [TestCase]
        public void TestClear()
        {
            FasterList<string> list = new FasterList<string>();
            list.Add("value");
            var capacity = list.capacity;

            list.Clear();

            Assert.That(list.count, Is.EqualTo(0));
            Assert.That(list.capacity, Is.EqualTo(capacity));
        }
    }
}
