using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class RingBufferTests
    {
        [Test]
        public void RingBuffer_OverwriteOnFull_DropsOldest()
        {
            var rb = new RingBuffer<int>(4);

            rb.Enqueue(1);
            rb.Enqueue(2);
            rb.Enqueue(3);
            rb.Enqueue(4);

            Assert.That(rb.count, Is.EqualTo(4));

            rb.Enqueue(5);
            Assert.That(rb.count, Is.EqualTo(4));

            Assert.That(rb.Dequeue(), Is.EqualTo(2));
            Assert.That(rb.Dequeue(), Is.EqualTo(3));
            Assert.That(rb.Dequeue(), Is.EqualTo(4));
            Assert.That(rb.Dequeue(), Is.EqualTo(5));
        }

        [Test]
        public void RingBuffer_DequeueEmpty_Throws()
        {
            var rb = new RingBuffer<int>(4);

            Assert.That(() => rb.Dequeue(), Throws.Exception);
        }

        [Test]
        public void RingBuffer_CopyTo_WritesInLogicalOrder()
        {
            var rb = new RingBuffer<int>(4);
            rb.Enqueue(1);
            rb.Enqueue(2);
            rb.Enqueue(3);

            var dst = new int[rb.count];
            rb.CopyTo(dst);

            Assert.That(dst, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void RingBuffer_Enumerator_IteratesSnapshot()
        {
            var rb = new RingBuffer<int>(4);
            rb.Enqueue(1);
            rb.Enqueue(2);

            var e = rb.GetEnumerator();
            Assert.That(e.MoveNext(), Is.True);
            Assert.That(e.Current, Is.EqualTo(1));

            // mutation after enumerator creation should not affect enumerator snapshot length
            rb.Enqueue(3);

            Assert.That(e.MoveNext(), Is.True);
            Assert.That(e.Current, Is.EqualTo(2));

            Assert.That(e.MoveNext(), Is.False);
        }

        [Test]
        public void CircularQueue_EmptyDequeue_Throws()
        {
            var q = new CircularQueue<int>(4);

            Assert.That(() => q.Dequeue(), Throws.Exception);
        }

        [Test]
        public void CircularQueue_FullEnqueue_Throws()
        {
            var q = new CircularQueue<int>(4);

            // capacity is power-of-two; queue uses one slot empty to distinguish full
            q.Enqueue(1);
            q.Enqueue(2);
            q.Enqueue(3);

            Assert.That(() => q.Enqueue(4), Throws.Exception);
        }

        [Test]
        public void CircularQueue_CopyTo_WrapAround_Works()
        {
            var q = new CircularQueue<int>(4);

            q.Enqueue(1);
            q.Enqueue(2);
            q.Enqueue(3);

            Assert.That(q.Dequeue(), Is.EqualTo(1));

            q.Enqueue(4);

            var dst = new int[q.count];
            q.CopyTo(dst);

            Assert.That(dst, Is.EqualTo(new[] { 2, 3, 4 }));
        }

        [Test]
        public void CircularQueue_Enumerator_Works()
        {
            var q = new CircularQueue<int>(4);
            q.Enqueue(1);
            q.Enqueue(2);

            var e = q.GetEnumerator();
            Assert.That(e.MoveNext(), Is.True);
            Assert.That(e.Current, Is.EqualTo(1));
            Assert.That(e.MoveNext(), Is.True);
            Assert.That(e.Current, Is.EqualTo(2));
            Assert.That(e.MoveNext(), Is.False);
        }

        [Test]
        public void UnmanagedCircularQueue_TryEnqueueTryDequeue_Works()
        {
            var q = new UnmanagedCircularQueue<long>(2);

            Assert.That(q.TryEnqueue(10L), Is.True);
            Assert.That(q.TryEnqueue(11L), Is.True);
            Assert.That(q.TryEnqueue(12L), Is.False);

            Assert.That(q.TryDequeue(out var v0), Is.True);
            Assert.That(v0, Is.EqualTo(10L));
            Assert.That(q.TryDequeue(out var v1), Is.True);
            Assert.That(v1, Is.EqualTo(11L));
            Assert.That(q.TryDequeue(out _), Is.False);
        }

        [Test]
        public void UnmanagedCircularQueue_RefEnumerator_IteratesLiveCells()
        {
            var q = new UnmanagedCircularQueue<long>(4);

            q.TryEnqueue(1L);
            q.TryEnqueue(2L);
            q.TryEnqueue(3L);

            var sum = 0L;
            var e = q.GetRefEnumerator();
            while (e.MoveNext())
                sum += e.Current;

            Assert.That(sum, Is.EqualTo(6L));
        }

        [Test]
        public void UnmanagedConcurrentCircularQueue_EnqueueDequeue_SingleThread_Works()
        {
            var q = new UnmanagedConcurrentCircularQueue<long>(2);

            Assert.That(q.TryDequeue(out _), Is.False);

            Assert.That(q.TryEnqueue(1L), Is.True);
            Assert.That(q.TryEnqueue(2L), Is.True);
            Assert.That(q.TryEnqueue(3L), Is.False);

            Assert.That(q.TryDequeue(out var a), Is.True);
            Assert.That(a, Is.EqualTo(1L));

            Assert.That(q.TryDequeue(out var b), Is.True);
            Assert.That(b, Is.EqualTo(2L));

            Assert.That(q.TryDequeue(out _), Is.False);
        }

        [Test]
        public void UnmanagedConcurrentCircularQueue_EnqueueDequeue_WithSizeOverload_Works()
        {
            // use a cell that is bigger than int
            var q = new UnmanagedConcurrentCircularQueue<long>(2);

            Assert.That(q.TryEnqueue(123L, sizeof(int)), Is.True);
            Assert.That(q.TryDequeue(out var v, sizeof(int)), Is.True);

            var roundtrip = unchecked((int)v);
            Assert.That(roundtrip, Is.EqualTo(123));
        }

        [Test]
        public void UnmanagedCircularQueue_EnqueueDequeue_WithSizeOverload_Works()
        {
            var q = new UnmanagedCircularQueue<long>(2);

            Assert.That(q.TryEnqueue(123L, sizeof(int)), Is.True);
            Assert.That(q.TryDequeue(out var v, sizeof(int)), Is.True);

            var roundtrip = unchecked((int)v);
            Assert.That(roundtrip, Is.EqualTo(123));
        }
    }
}
