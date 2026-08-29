using System;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class UnmanagedDataStructuresTests
    {
        [Test]
        public void NativeBag_EnqueueDequeue_Roundtrip_SingleType()
        {
            var bag = new NativeBag(Allocator.Persistent);
            try
            {
                Assert.That(bag.IsEmpty(), Is.True);

                bag.Enqueue(1);
                bag.Enqueue(2);
                bag.Enqueue(3);

                Assert.That(bag.IsEmpty(), Is.False);
                Assert.That(bag.count, Is.EqualTo(12)); // 3 * sizeof(int)

                Assert.That(bag.Dequeue<int>(), Is.EqualTo(1));
                Assert.That(bag.Dequeue<int>(), Is.EqualTo(2));
                Assert.That(bag.Dequeue<int>(), Is.EqualTo(3));

                Assert.That(bag.IsEmpty(), Is.True);
                Assert.That(bag.count, Is.EqualTo(0));
            }
            finally
            {
                bag.Dispose();
            }
        }

        [Test]
        public void NativeBag_ReserveEnqueue_AccessReserved_AllowsInPlaceUpdate()
        {
            var bag = new NativeBag(Allocator.Persistent);
            try
            {
                ref int reserved = ref bag.ReserveEnqueue<int>(out UnsafeArrayIndex idx);
                reserved = 10;

                ref int same = ref bag.AccessReserved<int>(idx);
                same = 99;

                Assert.That(bag.Dequeue<int>(), Is.EqualTo(99));
                Assert.That(bag.IsEmpty(), Is.True);
            }
            finally
            {
                bag.Dispose();
            }
        }

        [Test]
        public void NativeDynamicArray_Add_Get_Set_Remove_Clear_ToManagedArray_Work()
        {
            var arr = NativeDynamicArray.Alloc<int>(Allocator.Persistent, 2);
            try
            {
                Assert.That(arr.isValid, Is.True);
                Assert.That(arr.Count<int>(), Is.EqualTo(0));
                Assert.That(arr.Capacity<int>(), Is.GreaterThanOrEqualTo(2));

                arr.Add(10);
                arr.Add(11);
                arr.Add(12); // should grow from initial 2

                Assert.That(arr.Count<int>(), Is.EqualTo(3));
                Assert.That(arr.Get<int>(0), Is.EqualTo(10));
                Assert.That(arr.Get<int>(2), Is.EqualTo(12));

                arr.Set<int>(1, 999);
                Assert.That(arr.Get<int>(1), Is.EqualTo(999));

                // RemoveAt shifts down
                arr.RemoveAt<int>(1);
                Assert.That(arr.Count<int>(), Is.EqualTo(2));
                Assert.That(arr.Get<int>(0), Is.EqualTo(10));
                Assert.That(arr.Get<int>(1), Is.EqualTo(12));

                // UnorderedRemoveAt swaps with last
                arr.Add(20);
                arr.Add(21);
                arr.UnorderedRemoveAt<int>(0);
                Assert.That(arr.Count<int>(), Is.EqualTo(3));

                var managed = arr.ToManagedArray<int>();
                Assert.That(managed.Length, Is.EqualTo(3));

                arr.Clear();
                Assert.That(arr.Count<int>(), Is.EqualTo(0));
            }
            finally
            {
                arr.Dispose();
            }
        }

#if DEBUG
        [Test]
        public void NativeDynamicArray_AddWithoutGrow_Throws_WhenNoSpace()
        {
            var arr = NativeDynamicArray.Alloc<int>(Allocator.Persistent, 1);
            try
            {
                arr.AddWithoutGrow(1);

                Assert.That(() => arr.AddWithoutGrow(2), Throws.Exception);
            }
            finally
            {
                arr.Dispose();
            }
        }
#endif

        [Test]
        public void NativeDynamicArrayCast_Wraps_NativeDynamicArray()
        {
            var cast = new NativeDynamicArrayCast<int>(2, Allocator.Persistent);
            try
            {
                Assert.That(cast.isValid, Is.True);

                cast.Add(1);
                cast.Add(2);
                cast.Add(3);

                Assert.That(cast.count, Is.EqualTo(3));
                Assert.That(cast[2], Is.EqualTo(3));

                cast.RemoveAt(1);
                Assert.That(cast.count, Is.EqualTo(2));

                cast.UnorderedRemoveAt(0);
                Assert.That(cast.count, Is.EqualTo(1));

                cast.Clear();
                Assert.That(cast.count, Is.EqualTo(0));
            }
            finally
            {
                cast.Dispose();
            }
        }

        [Test]
        public void SharedNativeInt_Increment_Decrement_Add_CompareExchange_Set_Work()
        {
            var s = SharedNativeInt.Create(10, Allocator.Persistent);
            try
            {
                Assert.That((int)s, Is.EqualTo(10));

                Assert.That(s.Increment(), Is.EqualTo(11));
                Assert.That(s.Add(5), Is.EqualTo(16));
                Assert.That(s.Decrement(), Is.EqualTo(15));

                var old = s.CompareExchange(99, 15);
                Assert.That(old, Is.EqualTo(15));
                Assert.That((int)s, Is.EqualTo(99));

                s.Set(123);
                Assert.That((int)s, Is.EqualTo(123));
            }
            finally
            {
                s.Dispose();
            }
        }

        struct DummyDisposable : IDisposable
        {
            public int value;
            public int disposed;

            public void Dispose()
            {
                disposed = 1;
            }
        }

        [Test]
        public void SharedDisposableNative_Value_And_Dispose_Work()
        {
            var d = new DummyDisposable { value = 7, disposed = 0 };
            var s = new SharedDisposableNative<DummyDisposable>(d);
            try
            {
                ref var v = ref s.value;
                Assert.That(v.value, Is.EqualTo(7));

                v.value = 99;
                Assert.That(s.value.value, Is.EqualTo(99));
            }
            finally
            {
                s.Dispose();
            }
        }

        [Test]
        public void SharedDisposableNative_Value_Throws_IfNotInitialized()
        {
            var s = new SharedDisposableNative<DummyDisposable>();

            Assert.That(() =>
            {
                ref var v = ref s.value;
                _ = v.value;
            }, Throws.Exception);
        }
    }
}
