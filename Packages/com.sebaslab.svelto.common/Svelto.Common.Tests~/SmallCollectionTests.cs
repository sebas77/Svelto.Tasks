using System;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class SmallCollectionTests
    {
        [Test]
        public void SpanList_Add_AddRange_Indexer_AndAsSpan_Work()
        {
            Span<int> storage = stackalloc int[4];
            var list = new SpanList<int>(storage);

            list.Add(10);
            Span<int> additional = stackalloc int[] { 20, 30 };
            list.AddRange(additional);
            list[1] = 99;

            Assert.That(list.count, Is.EqualTo(3));
            Assert.That(list.Capacity, Is.EqualTo(4));
            Assert.That(list.AsSpan().ToArray(), Is.EqualTo(new[] { 10, 99, 30 }));
        }

        [Test]
        public void SpanList_RejectsOverflowAndIndexesOutsideCount()
        {
            Assert.That(AddPastSpanListCapacity, Throws.TypeOf<InvalidOperationException>());
            Assert.That(ReadNegativeSpanListIndex, Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(ReadIndexAtSpanListCount, Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SpanList_AddRangeRejectsInsufficientRemainingCapacity()
        {
            Assert.That(AddRangePastSpanListCapacity, Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void FasterListPool_ReleaseClearsAndReusesListOnCurrentThread()
        {
            var original = FasterListPool<int>.Get();
            original.Add(10);
            FasterListPool<int>.Release(original);

            var reused = FasterListPool<int>.Get();
            try
            {
                Assert.That(reused, Is.SameAs(original));
                Assert.That(reused.count, Is.Zero);
            }
            finally
            {
                FasterListPool<int>.Release(reused);
            }
        }

        [Test]
        public void FasterListPool_ReleaseNullDoesNothing()
        {
            Assert.That(() => FasterListPool<int>.Release(null), Throws.Nothing);
        }

        [Test]
        public void ByteArraySegment_ArrayAndMemoryBackingsExposeTypedSpan()
        {
            var ints = new[] { 10, 20 };
            var arraySegment = new ByteArraySegment<int>(ints);
            arraySegment.Span[1] = 99;

            var bytes = new byte[sizeof(int) * 2];
            var memorySegment = new ByteArraySegment<int>(bytes.AsMemory());
            memorySegment.Span[0] = 42;
            ReadOnlySpan<int> readOnly = memorySegment;

            Assert.That(ints, Is.EqualTo(new[] { 10, 99 }));
            Assert.That(readOnly.Length, Is.EqualTo(2));
            Assert.That(readOnly[0], Is.EqualTo(42));
        }

        [Test]
        public void ManagedStream_ReadByteArraySegment_RoundtripsWrittenSpan()
        {
            var bytes = new byte[64];
            var stream = new ManagedStream(bytes, bytes.Length);
            Span<int> values = stackalloc int[] { 10, 20, 30 };
            stream.WriteSpan(values);
            stream.Reset();

            var segment = stream.ReadByteArraySegment<int>();

            Assert.That(segment.Span.ToArray(), Is.EqualTo(new[] { 10, 20, 30 }));
        }

        static void AddPastSpanListCapacity()
        {
            Span<int> storage = stackalloc int[1];
            var list = new SpanList<int>(storage);
            list.Add(10);
            list.Add(20);
        }

        static void ReadNegativeSpanListIndex()
        {
            Span<int> storage = stackalloc int[1];
            var list = new SpanList<int>(storage);
            list.Add(10);
            var ignored = list[-1];
        }

        static void ReadIndexAtSpanListCount()
        {
            Span<int> storage = stackalloc int[1];
            var list = new SpanList<int>(storage);
            list.Add(10);
            var ignored = list[1];
        }

        static void AddRangePastSpanListCapacity()
        {
            Span<int> storage = stackalloc int[2];
            var list = new SpanList<int>(storage);
            list.Add(1);
            Span<int> additional = stackalloc int[] { 2, 3 };
            list.AddRange(additional);
        }
    }
}
