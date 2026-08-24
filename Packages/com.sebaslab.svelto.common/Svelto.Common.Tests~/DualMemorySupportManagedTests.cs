using System;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class DualMemorySupportManagedTests
    {
        [Test]
        public void MB_CopyFrom_CopyTo_Clear_Work()
        {
            var arr = new int[4];
            var mb = new MB<int>();
            mb.Set(arr);

            Assert.That(mb.capacity, Is.EqualTo(4));
            Assert.That(mb.isValid, Is.True);

            mb.CopyFrom(new[] { 1, 2, 3, 4 }, 4);

            var dst = new int[4];
            mb.CopyTo(0, dst, 0, 4);

            Assert.That(dst, Is.EqualTo(new[] { 1, 2, 3, 4 }));

            mb.Clear();
            Assert.That(arr, Is.EqualTo(new[] { 0, 0, 0, 0 }));
        }

        [Test]
        public void ManagedStrategy_Alloc_Resize_ShiftLeft_ShiftRight_Work()
        {
            var s = new ManagedStrategy<int>();
            s.Alloc(4, Allocator.Managed, memClear: true);

            Assert.That(s.isValid, Is.True);
            Assert.That(s.capacity, Is.EqualTo(4));

            s[0] = 10;
            s[1] = 11;
            s[2] = 12;
            s[3] = 13;

            // shift right index 1..3 -> moves [1..2] to [2..3]
            s.ShiftRight(1, 3);
            Assert.That(s[2], Is.EqualTo(11));

            // shift left index 0..2 -> moves [1..2] to [0..1]
            s.ShiftLeft(0, 2);
            Assert.That(s[0], Is.EqualTo(11));

            s.Resize(8, copyContent: true, memClear: false);
            Assert.That(s.capacity, Is.EqualTo(8));

            s.Clear();
            Assert.That(s[0], Is.EqualTo(0));

            // FastClear should clear for managed (reference or contains refs); for int it may no-op.
            // Just assert it doesn't throw.
            s.FastClear();
        }

        sealed class RefType
        {
            public int v;
        }

        [Test]
        public void ManagedStrategy_FastClear_Clears_ForReferenceTypes()
        {
            var s = new ManagedStrategy<RefType>();
            s.Alloc(2, Allocator.Managed, memClear: true);

            s[0] = new RefType { v = 1 };
            s[1] = new RefType { v = 2 };

            s.FastClear();

            Assert.That(s[0], Is.Null);
            Assert.That(s[1], Is.Null);
        }

        [Test]
        public void ManagedStrategy_ToBuffer_ThrowsIfNotAllocated()
        {
            var s = new ManagedStrategy<int>();

            Assert.That(() => ((IBufferStrategy<int>)s).ToBuffer(), Throws.Exception);
        }

        [Test]
        public void ManagedStrategy_AsBytesPointer_And_SerialiseFrom_ThrowNotImplemented()
        {
            var s = new ManagedStrategy<int>();
            s.Alloc(1, Allocator.Managed, memClear: true);

            Assert.That(() => s.AsBytesPointer(), Throws.TypeOf<NotImplementedException>());
            Assert.That(() => s.SerialiseFrom(IntPtr.Zero), Throws.TypeOf<NotImplementedException>());
        }
    }
}
