using System;
using System.Runtime.InteropServices;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    public partial class NativeBagTests
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

    }

    public partial class NativeDynamicArrayTests
    {
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
        public void NativeDynamicArray_SetWithinCapacity_ExpandsCount()
        {
            var array = NativeDynamicArray.Alloc<uint>(Allocator.Persistent, 20);
            try
            {
                array.Set(10, 42u);

                Assert.That(array.Count<uint>(), Is.EqualTo(11));
                Assert.That(array.Get<uint>(10), Is.EqualTo(42u));
            }
            finally
            {
                array.Dispose();
            }
        }

        [Test]
        public void NativeDynamicArray_ByteValues_GrowAndRoundtrip()
        {
            var array = NativeDynamicArray.Alloc<byte>(Allocator.Persistent);
            try
            {
                for (byte i = 0; i < 33; i++)
                    array.Add(i);

                Assert.That(array.Count<byte>(), Is.EqualTo(33));
                for (var i = 0; i < 33; i++)
                    Assert.That(array.Get<byte>(i), Is.EqualTo((byte)i));
            }
            finally
            {
                array.Dispose();
            }
        }

        [Test]
        public void NativeDynamicArrayCast_ByteValues_CanResizeAndRemove()
        {
            var array = new NativeDynamicArrayCast<byte>(0, Allocator.Persistent);
            try
            {
                array.Resize(10);
                for (byte i = 0; i < 10; i++)
                    array.Add(i);

                array.RemoveAt(3);
                Assert.That(array[3], Is.EqualTo((byte)4));

                array.UnorderedRemoveAt(0);

                Assert.That(array.count, Is.EqualTo(8));
                Assert.That(array[0], Is.EqualTo((byte)9));
                Assert.That(array[2], Is.EqualTo((byte)2));

                array.Resize(0);

                Assert.That(array.capacity, Is.EqualTo(0));
                Assert.That(array.count, Is.EqualTo(0));
            }
            finally
            {
                array.Dispose();
            }
        }

    }

    public partial class NativeBagTests
    {
        [Test]
        public void NativeBag_RoundtripsExplicitAndPackedStructs()
        {
            var bag = new NativeBag(Allocator.Persistent);
            try
            {
                var explicitValue = new ExplicitValue { first = 13, second = 1023, third = 2356 };
                var packedValue = new PackedValue { first = 13, second = 1023, third = 2356 };

                bag.Enqueue(explicitValue);
                bag.Enqueue(packedValue);

                var actualExplicit = bag.Dequeue<ExplicitValue>();
                var actualPacked = bag.Dequeue<PackedValue>();

                Assert.That(actualExplicit.first, Is.EqualTo(explicitValue.first));
                Assert.That(actualExplicit.second, Is.EqualTo(explicitValue.second));
                Assert.That(actualExplicit.third, Is.EqualTo(explicitValue.third));
                Assert.That(actualPacked.first, Is.EqualTo(packedValue.first));
                Assert.That(actualPacked.second, Is.EqualTo(packedValue.second));
                Assert.That(actualPacked.third, Is.EqualTo(packedValue.third));
            }
            finally
            {
                bag.Dispose();
            }
        }

        [Test]
        public void NativeBag_PreservesFifoOrder_AfterWrappingAndGrowing()
        {
            var bag = new NativeBag(Allocator.Persistent);
            try
            {
                for (var i = 0; i < 32; i++)
                    bag.Enqueue(i);

                for (var i = 0; i < 16; i++)
                    Assert.That(bag.Dequeue<int>(), Is.EqualTo(i));

                for (var i = 32; i < 256; i++)
                    bag.Enqueue(i);

                for (var i = 16; i < 256; i++)
                    Assert.That(bag.Dequeue<int>(), Is.EqualTo(i));

                Assert.That(bag.IsEmpty(), Is.True);
            }
            finally
            {
                bag.Dispose();
            }
        }

        [Test]
        public void NativeBag_ReservedValues_RemainAccessible_AfterWrappingAndGrowing()
        {
            var bag = new NativeBag(Allocator.Persistent);
            try
            {
                for (byte i = 0; i < 64; i++)
                    bag.Enqueue(i);

                for (var i = 0; i < 32; i++)
                    Assert.That(bag.Dequeue<byte>(), Is.EqualTo((byte)i));

                var indexes = new UnsafeArrayIndex[16];
                for (var i = 0; i < indexes.Length; i++)
                    bag.ReserveEnqueue<byte>(out indexes[i]) = (byte)(100 + i);

                for (byte i = 0; i < 128; i++)
                    bag.Enqueue(i);

                for (var i = 0; i < indexes.Length; i++)
                    Assert.That(bag.AccessReserved<byte>(indexes[i]), Is.EqualTo((byte)(100 + i)));
            }
            finally
            {
                bag.Dispose();
            }
        }

        [Test]
        public void NativeBag_DequeueBeyondAvailableData_Throws()
        {
            var bag = new NativeBag(Allocator.Persistent);
            try
            {
                bag.Enqueue((byte)0);
                bag.Enqueue((byte)1);

                bag.Dequeue<byte>();
                bag.Dequeue<byte>();

#if DEBUG
                Assert.That(() => bag.Dequeue<byte>(), Throws.Exception);
#else
                Assert.That(bag.count, Is.Zero);
#endif
            }
            finally
            {
                bag.Dispose();
            }
        }

        [Test]
        public void NativeBag_PreservesFifoOrder_WhenWriterWrapsBeforeReader()
        {
            var bag = new NativeBag(Allocator.Persistent);
            try
            {
                for (uint i = 0; i < 16; i++)
                    bag.Enqueue(i);

                for (uint i = 0; i < 8; i++)
                    Assert.That(bag.Dequeue<uint>(), Is.EqualTo(i));

                for (uint i = 16; i < 25; i++)
                    bag.Enqueue(i);

                for (uint i = 8; i < 25; i++)
                    Assert.That(bag.Dequeue<uint>(), Is.EqualTo(i));

                Assert.That(bag.IsEmpty(), Is.True);
            }
            finally
            {
                bag.Dispose();
            }
        }

    }

    [TestFixture]
    public class SharedNativeTests
    {
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

    [StructLayout(LayoutKind.Explicit, Size = 7)]
    struct ExplicitValue
    {
        [FieldOffset(0)] public byte first;
        [FieldOffset(1)] public short third;
        [FieldOffset(3)] public uint second;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct PackedValue
    {
        public byte first;
        public uint second;
        public short third;
    }
}
