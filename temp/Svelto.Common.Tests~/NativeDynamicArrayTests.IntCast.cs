using NUnit.Framework;
using Svelto.Common;
using Svelto.DataStructures;
using Assert = NUnit.Framework.Assert;

namespace Svelto.Common.Tests
{
    public partial class NativeDynamicArrayTests
    {
        [TestCase]
        public void TestAllocationSize0_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);

            Assert.That(fasterList.capacity, Is.EqualTo(0));
            Assert.That(fasterList.count, Is.EqualTo(0));
            
            fasterList.Dispose();
        }
        
        [TestCase]
        public void TestAllocationSize1_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(1, Allocator.Persistent);

            Assert.That(fasterList.capacity, Is.EqualTo(1));
            Assert.That(fasterList.count, Is.EqualTo(0));
            
            fasterList.Dispose();
        }
        
        [TestCase]
        public void TestResize_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
            
            fasterList.Resize(10);

            Assert.That(fasterList.capacity, Is.EqualTo(10));
            Assert.That(fasterList.count, Is.EqualTo(0));
            
            fasterList.Dispose();
        }
        
        [TestCase]
        public void TestResizeTo0_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(10, Allocator.Persistent);
            
            fasterList.Resize(0);

            Assert.That(fasterList.capacity, Is.EqualTo(0));
            Assert.That(fasterList.count, Is.EqualTo(0));
            
            fasterList.Dispose();
        }
        
        // [TestCase]
        // public void TestExpandTo()
        // {
        //     NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
        //     
        //     fasterList.ExpandTo(10);
        //
        //     Assert.That(fasterList.capacity, Is.EqualTo(10));
        //     Assert.That(fasterList.count, Is.EqualTo(10));
        //     
        //     fasterList.Dispose();
        // }
        //
        // [TestCase]
        // public void TestExpandByFromZero()
        // {
        //     NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
        //     
        //     fasterList.ExpandBy(10);
        //
        //     Assert.That(fasterList.capacity, Is.EqualTo(10));
        //     Assert.That(fasterList.count, Is.EqualTo(10));
        //     
        //     fasterList.Dispose();
        // }
        //
        // [TestCase]
        // public void TestSet()
        // {
        //     NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
        //     
        //     fasterList.ExpandTo(10);
        //     
        //     for (int i = 0; i < 10; i++)
        //         fasterList[i] = i;
        //     
        //     for (int i = 0; i < 10; i++)
        //         Assert.That(fasterList[i], Is.EqualTo(i));
        //     
        //     fasterList.Dispose();
        // }

        [TestCase]
        public void TestAdd_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);

            for (int i = 0; i < 10; i++)
                fasterList.Add(i);
            
            for (int i = 0; i < 10; i++)
                Assert.That(fasterList[i], Is.EqualTo(i));

            Assert.That(fasterList.capacity, Is.EqualTo(10));
            Assert.That(fasterList.count, Is.EqualTo(10));
            
            fasterList.Dispose();
        }
        
        // [TestCase]
        // public void TestExpandBy()
        // {
        //     NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(3, Allocator.Persistent);
        //     
        //     fasterList.Add(0);
        //     fasterList.Add(1);
        //     fasterList.Add(2);
        //     
        //     fasterList.ExpandBy(10);
        //
        //     Assert.That(fasterList.capacity, Is.EqualTo(13));
        //     Assert.That(fasterList.count, Is.EqualTo(13));
        //     
        //     fasterList.Dispose();
        // }
        //
        // [TestCase]
        // public void TestEnsureCapacity()
        // {
        //     NativeDynamicArrayCast<int> fasterListA = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
        //     
        //     fasterListA.EnsureCapacity(10);
        //     
        //     Assert.That(fasterListA.capacity, Is.EqualTo(10));
        //     Assert.That(fasterListA.count, Is.EqualTo(0));
        //     
        //     fasterListA.Dispose();
        // }
        //
        // [TestCase]
        // public void TestEnsureExtraCapacity()
        // {
        //     NativeDynamicArrayCast<int> fasterListA = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
        //     
        //     fasterListA.ExpandBy(10);
        //     fasterListA.EnsureExtraCapacity(10);
        //     
        //     Assert.That(fasterListA.capacity, Is.EqualTo(20));
        //     Assert.That(fasterListA.count, Is.EqualTo(10));
        //     
        //     fasterListA.Dispose();
        // }
        //
        // [TestCase]
        // public void TestPush()
        // {
        //     NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
        //
        //     for (int i = 0; i < 10; i++)
        //         fasterList.Push(i);
        //     
        //     for (int i = 0; i < 10; i++)
        //         Assert.That(fasterList[i], Is.EqualTo(i));
        //
        //     Assert.That(fasterList.capacity, Is.EqualTo(10));
        //     Assert.That(fasterList.count, Is.EqualTo(10));
        //     fasterList.Dispose();
        // }
        //
        // [TestCase]
        // public void TestPop()
        // {
        //     NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
        //
        //     for (int i = 0; i < 10; i++)
        //         fasterList.Push(i);
        //     
        //     for (int i = 9; i >= 0; i--)
        //         Assert.That(fasterList.Pop(), Is.EqualTo(i));
        //
        //     Assert.That(fasterList.capacity, Is.EqualTo(10));
        //     Assert.That(fasterList.count, Is.EqualTo(0));
        //     
        //     fasterList.Dispose();
        // }
        //
        // [TestCase]
        // public void TestPeek()
        // {
        //     NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
        //
        //     for (int i = 0; i < 10; i++)
        //         fasterList.Push(i);
        //     
        //     Assert.That(fasterList.Peek(), Is.EqualTo(9));
        //
        //     Assert.That(fasterList.capacity, Is.EqualTo(10));
        //     Assert.That(fasterList.count, Is.EqualTo(10));
        //     
        //     fasterList.Dispose();
        // }
        //
        
        [TestCase]
        public void TestRemoveAt_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);

            for (int i = 0; i < 10; i++)
                fasterList.Add(i);
            
            fasterList.RemoveAt(3);
            Assert.That(fasterList[3], Is.EqualTo(4));
            
            fasterList.RemoveAt(0);
            Assert.That(fasterList[0], Is.EqualTo(1));
            
            fasterList.RemoveAt(7);
            
            Assert.That(fasterList.capacity, Is.EqualTo(10));
            Assert.That(fasterList.count, Is.EqualTo(7));
            
            fasterList.Dispose();
        }
        
        [TestCase]
        public void TestUnorderedRemoveAt_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);

            for (int i = 0; i < 10; i++)
                fasterList.Add(i);
            
            fasterList.UnorderedRemoveAt(3);
            Assert.That(fasterList[3], Is.EqualTo(9));
            
            fasterList.UnorderedRemoveAt(0);
            Assert.That(fasterList[0], Is.EqualTo(8));
            
            fasterList.UnorderedRemoveAt(7);
            
            Assert.That(fasterList.capacity, Is.EqualTo(10));
            Assert.That(fasterList.count, Is.EqualTo(7));
            
            fasterList.Dispose();
        }
        
        // [TestCase]
        // public void TestTrim()
        // {
        //     NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
        //
        //     for (int i = 0; i < 10; i++)
        //         fasterList.Add(i);
        //     
        //     fasterList.UnorderedRemoveAt(3);
        //     fasterList.UnorderedRemoveAt(0);
        //     fasterList.UnorderedRemoveAt(7);
        //     
        //     fasterList.Trim();
        //     
        //     Assert.That(fasterList.capacity, Is.EqualTo(7));
        //     Assert.That(fasterList.count, Is.EqualTo(7));
        //     
        //     fasterList.Dispose();
        // }
        
        [TestCase]
        public void TestSetAt_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
            
            fasterList.AddAt(10) = 10;
            
            Assert.That(fasterList[10], Is.EqualTo(10));
            
            Assert.That(fasterList.capacity, Is.EqualTo(16));
            Assert.That(fasterList.count, Is.EqualTo(11));
            
            fasterList.Dispose();
        }
        
        [TestCase]
        public void TestContains_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
            fasterList.Add(10);

            Assert.That(fasterList.Contains(10));
            Assert.That(fasterList.Contains(20), Is.EqualTo(false));

            fasterList.Dispose();
        }
        [TestCase]
        public void TestFastClear_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
            fasterList.Add(10);
            fasterList.Add(20);
            var array = fasterList.ToNativeArray();

            array.MemClear();

            Assert.That(fasterList.count, Is.EqualTo(2));
            Assert.That(fasterList[0], Is.EqualTo(0));
            Assert.That(fasterList[1], Is.EqualTo(0));

            fasterList.Dispose();
        }
        [TestCase]
        public void TestResetToReuse_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
            fasterList.Add(10);
            var capacity = fasterList.capacity;

            fasterList.Clear();

            Assert.That(fasterList.count, Is.EqualTo(0));
            Assert.That(fasterList.capacity, Is.EqualTo(capacity));

            fasterList.Dispose();
        }
        
        [TestCase]
        public void TestReuseOneSlot_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
            fasterList.Add(10);
            var capacity = fasterList.capacity;
            fasterList.Clear();

            fasterList.Add(20);

            Assert.That(fasterList[0], Is.EqualTo(20));
            Assert.That(fasterList.capacity, Is.EqualTo(capacity));

            fasterList.Dispose();
        }
        [TestCase]
        public void TestCopyTo_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
            fasterList.Add(10);
            fasterList.Add(20);

            var copy = fasterList.ToNativeArray().ToManagedArray<int>();

            Assert.That(copy, Is.EqualTo(new[] { 10, 20 }));

            fasterList.Dispose();
        }
        [TestCase]
        public void TestClear_IntCast()
        {
            NativeDynamicArrayCast<int> fasterList = new NativeDynamicArrayCast<int>(0, Allocator.Persistent);
            fasterList.Add(10);
            var capacity = fasterList.capacity;

            fasterList.Clear();

            Assert.That(fasterList.count, Is.EqualTo(0));
            Assert.That(fasterList.capacity, Is.EqualTo(capacity));

            fasterList.Dispose();
        }
    }
}
