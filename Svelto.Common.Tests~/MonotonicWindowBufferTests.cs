using System;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class MonotonicWindowBufferTests
    {
        [Test]
        public void Ctor_ExpectedCountZero_Throws()
        {
            Assert.That(() => new MonotonicWindowBuffer<int>(0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Count_WhenNothingPublished_Returns0()
        {
            var buf = new MonotonicWindowBuffer<int>(4);

            Assert.That(buf.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryPeek_WhenHeadNotSet_Throws()
        {
            var buf = new MonotonicWindowBuffer<int>(4);

            Assert.That(() => buf.TryPeek(out _), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void TryDequeue_WhenHeadNotSet_Throws()
        {
            var buf = new MonotonicWindowBuffer<int>(4);

            Assert.That(() => buf.TryDequeue(out _), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void SetHead_Negative_Throws()
        {
            var buf = new MonotonicWindowBuffer<int>(4);

            Assert.That(() => buf.SetHead(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SetHead_CannotMoveBackwards_Throws()
        {
            var buf = new MonotonicWindowBuffer<int>(4);

            buf.SetHead(10);

            Assert.That(() => buf.SetHead(9), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Set_And_TryGet_Work()
        {
            var buf = new MonotonicWindowBuffer<int>(4);
            buf.SetHead(5);

            Assert.That(buf.Add(5, 123), Is.True);
            Assert.That(buf.TryGet(5, out var value), Is.EqualTo(MonotonicSlotState.Published));
            Assert.That(value, Is.EqualTo(123));
        }

        [Test]
        public void TryGet_Unpublished_ReturnsNotPublished()
        {
            var buf = new MonotonicWindowBuffer<int>(4);
            buf.SetHead(0);

            Assert.That(buf.TryGet(0, out _), Is.EqualTo(MonotonicSlotState.NotPublished));
        }

        [Test]
        public void Peek_And_Dequeue_RequireHeadAndPublished()
        {
            var buf = new MonotonicWindowBuffer<int>(4);
            buf.SetHead(0);

            Assert.That(buf.TryPeek(out _), Is.False);
            Assert.That(buf.TryDequeue(out _), Is.False);

            buf.Add(0, 10);
            buf.Add(1, 11);

            Assert.That(buf.TryPeek(out var v0), Is.True);
            Assert.That(v0, Is.EqualTo(10));

            Assert.That(buf.TryDequeue(out var d0), Is.True);
            Assert.That(d0, Is.EqualTo(10));

            Assert.That(buf.TryPeek(out var v1), Is.True);
            Assert.That(v1, Is.EqualTo(11));

            Assert.That(buf.TryDequeue(out var d1), Is.True);
            Assert.That(d1, Is.EqualTo(11));

            Assert.That(buf.TryPeek(out _), Is.False);
        }

        [Test]
        public void Set_ForIndexBelowHead_ReturnsFalse()
        {
            var buf = new MonotonicWindowBuffer<int>(4);
            buf.SetHead(10);

            Assert.That(buf.Add(9, 1), Is.False);
        }

        [Test]
        public void Set_ForIndexOutsideWindow_Throws()
        {
            var buf = new MonotonicWindowBuffer<int>(4);
            buf.SetHead(0);

            Assert.That(() => buf.Add(4, 1), Throws.TypeOf<MonotonicWindowBufferOverflowException>());
        }

        [Test]
        public void Count_AfterPublishingRange_ReturnsSpanLengthFromHeadToHighest()
        {
            var buf = new MonotonicWindowBuffer<int>(8);
            buf.SetHead(10);

            Assert.That(buf.Count, Is.EqualTo(0));

            buf.Add(10, 10);
            Assert.That(buf.Count, Is.EqualTo(1));

            buf.Add(12, 12);
            Assert.That(buf.Count, Is.EqualTo(3));

            // dequeue head -> head becomes 11
            Assert.That(buf.TryDequeue(out _), Is.True);
            Assert.That(buf.Count, Is.EqualTo(2));
        }
    }
}
