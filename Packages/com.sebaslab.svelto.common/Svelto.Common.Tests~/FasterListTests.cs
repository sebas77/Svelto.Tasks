using System;
using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public partial class FasterListTests
    {
        [Test]
        public void Add_And_Indexer_Work()
        {
            var list = new FasterList<int>();

            list.Add(1);
            list.Add(2);
            list.Add(3);

            Assert.That(list.count, Is.EqualTo(3));
            Assert.That(list[0], Is.EqualTo(1));
            Assert.That(list[1], Is.EqualTo(2));
            Assert.That(list[2], Is.EqualTo(3));
        }

        [Test]
        public void AddAt_ExpandsCount_And_SetsValue()
        {
            var list = new FasterList<int>();

            list.AddAt(4, 123);

            Assert.That(list.count, Is.EqualTo(5));
            Assert.That(list[4], Is.EqualTo(123));
        }

        [Test]
        public void GetOrCreate_InvokesFactoryOnlyWhenDefault()
        {
            var list = new FasterList<string>();
            var calls = 0;

            ref string a = ref list.GetOrCreate(0, () =>
            {
                calls++;
                return "hello";
            });

            ref string b = ref list.GetOrCreate(0, () =>
            {
                calls++;
                return "world";
            });

            Assert.That(list.count, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(a, Is.EqualTo("hello"));
            Assert.That(b, Is.EqualTo("hello"));
        }

        [Test]
        public void InsertAt_InsertsAndShiftsRight()
        {
            var list = new FasterList<int>();
            list.Add(1);
            list.Add(3);

            list.InsertAt(1, 2);

            Assert.That(list.count, Is.EqualTo(3));
            Assert.That(list[0], Is.EqualTo(1));
            Assert.That(list[1], Is.EqualTo(2));
            Assert.That(list[2], Is.EqualTo(3));
        }

        [Test]
        public void RemoveAt_RemovesAndShiftsLeft()
        {
            var list = new FasterList<int>();
            list.Add(1);
            list.Add(2);
            list.Add(3);

            list.RemoveAt(1);

            Assert.That(list.count, Is.EqualTo(2));
            Assert.That(list[0], Is.EqualTo(1));
            Assert.That(list[1], Is.EqualTo(3));
        }

        [Test]
        public void UnorderedRemoveAt_WhenNotLast_ReturnsTrue_AndSwapsLastIntoHole()
        {
            var list = new FasterList<int>();
            list.Add(10);
            list.Add(20);
            list.Add(30);

            var swapped = list.UnorderedRemoveAt(0);

            Assert.That(swapped, Is.True);
            Assert.That(list.count, Is.EqualTo(2));
            Assert.That(list[0], Is.EqualTo(30));
            Assert.That(list[1], Is.EqualTo(20));
        }

        [Test]
        public void UnorderedRemoveAt_WhenLast_ReturnsFalse_AndClearsSlot()
        {
            var list = new FasterList<int>();
            list.Add(10);
            list.Add(20);
            list.Add(30);

            var swapped = list.UnorderedRemoveAt(2);

            Assert.That(swapped, Is.False);
            Assert.That(list.count, Is.EqualTo(2));
            Assert.That(list[0], Is.EqualTo(10));
            Assert.That(list[1], Is.EqualTo(20));
        }

        [Test]
        public void TrimCount_ShrinksCountOnly()
        {
            var list = new FasterList<int>(10);
            for (var i = 0; i < 5; i++)
                list.Add(i);

            list.TrimCount(2);

            Assert.That(list.count, Is.EqualTo(2));
            Assert.That(list[0], Is.EqualTo(0));
            Assert.That(list[1], Is.EqualTo(1));
        }

        [Test]
        public void ToArray_ReturnsCopyOfCountElements()
        {
            var list = new FasterList<int>();
            list.Add(7);
            list.Add(8);

            var arr = list.ToArray();

            Assert.That(arr, Is.EqualTo(new[] { 7, 8 }));

            // ensure copy
            arr[0] = 999;
            Assert.That(list[0], Is.EqualTo(7));
        }

        [Test]
        public void FasterReadOnlyList_And_LocalFasterReadOnlyList_Work()
        {
            var list = new FasterList<int>();
            list.Add(1);
            list.Add(2);

            FasterReadOnlyList<int> ro = list;
            Assert.That(ro.count, Is.EqualTo(2));
            Assert.That(ro[0], Is.EqualTo(1));

            LocalFasterReadOnlyList<int> local = ro;
            Assert.That(local.count, Is.EqualTo(2));
            Assert.That(local[1], Is.EqualTo(2));

            var arr = local.ToArray();
            Assert.That(arr, Is.EqualTo(new[] { 1, 2 }));
        }

#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER
        [Test]
        public void FasterListExtension_ToSpan_And_ToByteSpan_Work()
        {
            var list = new FasterList<int>();
            list.Add(1);
            list.Add(2);
            list.Add(3);

            Span<int> span = list.ToSpan();
            Assert.That(span.Length, Is.EqualTo(3));
            Assert.That(span[2], Is.EqualTo(3));

            Span<byte> bytes = list.ToByteSpan();
            Assert.That(bytes.Length, Is.EqualTo(sizeof(int) * 3));
        }

        [Test]
        public void FasterListExtension_CopyFrom_Array_Works()
        {
            var src = new[] { 1, 2, 3, 4 };
            var dst = new FasterList<int>();

            dst.CopyFrom(src);

            Assert.That(dst.count, Is.EqualTo(4));
            Assert.That(dst[0], Is.EqualTo(1));
            Assert.That(dst[3], Is.EqualTo(4));
        }

        [Test]
        public void FasterListExtension_CopyFrom_List_Works()
        {
            IList<int> src = new List<int> { 5, 6, 7 };
            var dst = new FasterList<int>();

            dst.CopyFrom(src);

            Assert.That(dst.count, Is.EqualTo(3));
            Assert.That(dst[1], Is.EqualTo(6));
        }

        [Test]
        public void FasterListExtension_CopyFrom_FasterList_And_ReadOnly_Works()
        {
            var src = new FasterList<int>();
            src.Add(1);
            src.Add(2);

            var dst = new FasterList<int>();
            dst.CopyFrom(src);

            Assert.That(dst.count, Is.EqualTo(2));
            Assert.That(dst[0], Is.EqualTo(1));

            FasterReadOnlyList<int> ro = src;
            var dst2 = new FasterList<int>();
            dst2.CopyFrom(ro);

            Assert.That(dst2.count, Is.EqualTo(2));
            Assert.That(dst2[1], Is.EqualTo(2));
        }
#endif

        [Test]
        public void Clear_ResetsCount_AndRetainsCapacity()
        {
            var list = new FasterList<string>(2);
            list.Add("value");

            list.Clear();

            Assert.That(list.count, Is.EqualTo(0));
            Assert.That(list.capacity, Is.EqualTo(2));
        }
    }
}
