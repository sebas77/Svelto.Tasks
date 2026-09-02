using System;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class StreamsTests
    {
#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER
        [Test]
        public void SveltoStream_WriteRead_Roundtrip()
        {
            var stream = new SveltoStream(64);
            var buffer = new byte[64];
            var span = new Span<byte>(buffer);

            stream.Write(span, 123);
            stream.Write(span, 456);

            stream.Reset();

            Assert.That(stream.Read<int>(span), Is.EqualTo(123));
            Assert.That(stream.Read<int>(span), Is.EqualTo(456));
        }

        [Test]
        public void SveltoStream_WriteSpan_ReadSpan_Roundtrip()
        {
            var stream = new SveltoStream(128);
            var buffer = new byte[128];
            var span = new Span<byte>(buffer);

            Span<int> toWrite = stackalloc int[3];
            toWrite[0] = 1;
            toWrite[1] = 2;
            toWrite[2] = 3;

            stream.WriteSpan(span, toWrite);

            stream.Reset();

            var readSpan = stream.ReadSpan<int>(span);
            Assert.That(readSpan.Length, Is.EqualTo(3));
            Assert.That(readSpan[0], Is.EqualTo(1));
            Assert.That(readSpan[2], Is.EqualTo(3));
        }

        [Test]
        public void SveltoStream_ReadSpan_WhenLengthIsZero_ReturnsEmpty()
        {
            var stream = new SveltoStream(16);
            var buffer = new byte[16];
            var span = new Span<byte>(buffer);

            stream.Write(span, (ushort)0);

            stream.Reset();

            var readSpan = stream.ReadSpan<int>(span);
            Assert.That(readSpan.Length, Is.EqualTo(0));
        }

        [Test]
        public void SveltoStream_OverwriteAt_Works_And_InvalidOverwriteThrows()
        {
            var stream = new SveltoStream(64);
            var buffer = new byte[64];
            var span = new Span<byte>(buffer);

            stream.Write(span, 1);
            stream.Write(span, 2);

            stream.OverwriteAt(span, 999, 0);

            stream.Reset();
            Assert.That(stream.Read<int>(span), Is.EqualTo(999));
            Assert.That(stream.Read<int>(span), Is.EqualTo(2));

            Assert.That(() => stream.OverwriteAt(new Span<byte>(buffer), 1, 1000), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void SveltoStream_AdvanceCursor_OutOfBounds_Throws()
        {
            var stream = new SveltoStream(4);

            Assert.That(() => stream.AdvanceCursor(5), Throws.Exception);
        }

        [Test]
        public void ManagedStream_WriteRead_Roundtrip()
        {
            var bytes = new byte[64];
            var ms = new ManagedStream(bytes, bytes.Length);

            ms.Write(123);
            ms.Write(456);

            ms.Reset();

            Assert.That(ms.Read<int>(), Is.EqualTo(123));
            Assert.That(ms.Read<int>(), Is.EqualTo(456));
        }

        [Test]
        public void ManagedStream_AsSpan_ReturnsWrittenLength()
        {
            var bytes = new byte[64];
            var ms = new ManagedStream(bytes, bytes.Length);

            Assert.That(ms.AsSpan().Length, Is.EqualTo(0));

            ms.Write(123);
            Assert.That(ms.AsSpan().Length, Is.EqualTo(sizeof(int)));
        }

        [Test]
        public unsafe void UnmanagedStream_WriteRead_Roundtrip()
        {
            var buf = stackalloc byte[64];
            var us = new UnmanagedStream(buf, 64);

            us.Write(123);
            us.Write(456);

            us.Reset();

            Assert.That(us.Read<int>(), Is.EqualTo(123));
            Assert.That(us.Read<int>(), Is.EqualTo(456));
        }
#endif
    }
}
