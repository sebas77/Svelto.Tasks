using System;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class SentinelRWTests
    {
        [Test]
        public void MB_AsReader_ReadRead_IsAllowed()
        {
            var backing = new int[4];
            var mb = Svelto.DataStructures.MB<int>.Create(backing);

            using (var r1 = mb.AsReader())
            using (var r2 = mb.AsReader())
            {
                _ = r1[0];
                _ = r2[0];
            }
        }

        [Test]
        public void MB_AsWriter_WriteWhileRead_Throws()
        {
            var backing = new int[4];
            var mb = Svelto.DataStructures.MB<int>.Create(backing);

            var r = mb.AsReader();
            try
            {
                var thrown = false;
                try
                {
                    var w = mb.AsWriter();
                    w.Dispose();
                }
                catch (Exception)
                {
                    thrown = true;
                }

                Assert.That(thrown, Is.True);
                _ = r.capacity;
            }
            finally
            {
                r.Dispose();
            }
        }

        [Test]
        public void MB_AsWriter_WriteWhileWrite_Throws()
        {
            var backing = new int[4];
            var mb = Svelto.DataStructures.MB<int>.Create(backing);

            var w1 = mb.AsWriter();
            try
            {
                var thrown = false;
                try
                {
                    var w2 = mb.AsWriter();
                    w2.Dispose();
                }
                catch (Exception)
                {
                    thrown = true;
                }

                Assert.That(thrown, Is.True);
                _ = w1.capacity;
            }
            finally
            {
                w1.Dispose();
            }
        }

        [Test]
        public unsafe void NB_AsReader_ReadRead_IsAllowed()
        {
            var statePtr = (int*)MemoryUtilities.NativeAlloc(sizeof(int), Allocator.Persistent);
            *statePtr = 0;

            var dataPtr = (int*)MemoryUtilities.NativeAlloc(sizeof(int) * 4, Allocator.Persistent);
            dataPtr[0] = 1;

            var nb = Svelto.DataStructures.NB<int>.Create((IntPtr)dataPtr, 4, (IntPtr)statePtr);

            var r1 = nb.AsReader();
            try
            {
                var r2 = nb.AsReader();
                try
                {
                    _ = r1[0];
                    _ = r2[0];
                }
                finally
                {
                    r2.Dispose();
                }
            }
            finally
            {
                r1.Dispose();
            }

            MemoryUtilities.NativeFree((IntPtr)dataPtr, Allocator.Persistent);
            MemoryUtilities.NativeFree((IntPtr)statePtr, Allocator.Persistent);
        }

        [Test]
        public unsafe void NB_AsWriter_WriteWhileRead_Throws()
        {
            var statePtr = (int*)MemoryUtilities.NativeAlloc(sizeof(int), Allocator.Persistent);
            *statePtr = 0;

            var dataPtr = (int*)MemoryUtilities.NativeAlloc(sizeof(int) * 4, Allocator.Persistent);

            var nb = Svelto.DataStructures.NB<int>.Create((IntPtr)dataPtr, 4, (IntPtr)statePtr);

            var r = nb.AsReader();
            try
            {
                var thrown = false;
                try
                {
                    var w = nb.AsWriter();
                    w.Dispose();
                }
                catch (Exception)
                {
                    thrown = true;
                }

                Assert.That(thrown, Is.True);
                _ = r.capacity;
            }
            finally
            {
                r.Dispose();
            }

            MemoryUtilities.NativeFree((IntPtr)dataPtr, Allocator.Persistent);
            MemoryUtilities.NativeFree((IntPtr)statePtr, Allocator.Persistent);
        }

        [Test]
        public unsafe void NB_AsWriter_WriteWhileWrite_Throws()
        {
            var statePtr = (int*)MemoryUtilities.NativeAlloc(sizeof(int), Allocator.Persistent);
            *statePtr = 0;

            var dataPtr = (int*)MemoryUtilities.NativeAlloc(sizeof(int) * 4, Allocator.Persistent);

            var nb = Svelto.DataStructures.NB<int>.Create((IntPtr)dataPtr, 4, (IntPtr)statePtr);

            var w1 = nb.AsWriter();
            try
            {
                var thrown = false;
                try
                {
                    var w2 = nb.AsWriter();
                    w2.Dispose();
                }
                catch (Exception)
                {
                    thrown = true;
                }

                Assert.That(thrown, Is.True);
                _ = w1.capacity;
            }
            finally
            {
                w1.Dispose();
            }

            MemoryUtilities.NativeFree((IntPtr)dataPtr, Allocator.Persistent);
            MemoryUtilities.NativeFree((IntPtr)statePtr, Allocator.Persistent);
        }
    }
}
