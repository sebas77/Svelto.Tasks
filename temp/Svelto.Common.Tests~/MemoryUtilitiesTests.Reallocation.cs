using System.Runtime.CompilerServices;
using NUnit.Framework;
using Svelto.Common;

namespace Svelto.Common.Tests
{
    public partial class MemoryUtilitiesTests
    {
        struct Test
        {
            public int a;
            public int b;
        }

        [Test]
        public void TestResize()
        {
            unsafe
            {
                var ptr = MemoryUtilities.NativeAlloc(10, Allocator.Persistent);
                Unsafe.Write((void*) ptr, new Test()
                {
                    a = 3
                  , b = 1
                });
                Unsafe.Write((void*) (ptr + 8), (short) -10);
                ptr = MemoryUtilities.NativeRealloc(ptr, 10, Allocator.Persistent, 16);
                var test = Unsafe.Read<Test>((void*) ptr);
                Assert.That(test.a == 3);
                Assert.That(test.b == 1);
                Assert.That(Unsafe.Read<short>((void*) (ptr + 8)) == -10);
                MemoryUtilities.NativeFree(ptr, Allocator.Persistent);
            }
        }
    }
}
