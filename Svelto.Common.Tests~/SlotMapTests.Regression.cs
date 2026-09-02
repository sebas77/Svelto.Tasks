using Svelto.DataStructures;
using Svelto.DataStructures.Native;

namespace Svelto.Common.Tests
{
    public partial class SlotMapTests
    {
        [Test]
        public void Add_UsesInitialCapacity_BeforeGrowing()
        {
            var c = new ManagedSlotMap<int>(3);
            try
            {
                c.Add(10);
                c.Add(20);
                c.Add(30);

                Assert.That(c.capacity, Is.EqualTo(3));

                var fourth = c.Add(40);

                Assert.That(c.capacity, Is.GreaterThan(3));
                Assert.That(c[fourth], Is.EqualTo(40));
            }
            finally
            {
                c.Dispose();
            }
        }

        [Test]
        public void Clear_InvalidatesHandles_AndResetsTheFreeList()
        {
            var c = new ManagedSlotMap<int>(2);
            try
            {
                var first = c.Add(10);
                var second = c.Add(20);
                c.Remove(second);

                c.Clear();

                Assert.That(c.Has(first), Is.False);
                Assert.That(c.Has(second), Is.False);

                var next = c.Add(30);

                Assert.That(c.count, Is.EqualTo(1));
                Assert.That(c.Has(next), Is.True);
                Assert.That(c[next], Is.EqualTo(30));
                Assert.That(c.Has(first), Is.False);
                Assert.That(c.Has(second), Is.False);
            }
            finally
            {
                c.Dispose();
            }
        }

        [Test]
        public void GenerationExhaustion_RetiresSlotWithoutRevalidatingOldHandles()
        {
            var c = new ManagedSlotMap<int>(3);
            try
            {
                var handles = new ValueIndex[255];
                var current = c.Add(0);
                var stableA = c.Add(100);
                var stableB = c.Add(200);
                handles[0] = current;

                for (var generation = 1; generation < byte.MaxValue; generation++)
                {
                    c.Remove(current);
                    current = c.Add(generation);
                    handles[generation] = current;
                }

                c.Remove(current);
                var replacement = c.Add(999);

                Assert.That(c.count, Is.EqualTo(3));
                Assert.That(c.capacity, Is.GreaterThan(3));
                Assert.That(c[replacement], Is.EqualTo(999));
                Assert.That(c[stableA], Is.EqualTo(100));
                Assert.That(c[stableB], Is.EqualTo(200));

                foreach (var staleHandle in handles)
                    Assert.That(c.Has(staleHandle), Is.False);
            }
            finally
            {
                c.Dispose();
            }
        }

        [Test]
        public void Clear_InvalidatesHandles_WithNativeSparseStrategy()
        {
            var c = new SlotMap<int, ManagedStrategy<int>, NativeStrategy<SparseIndex>>(2);
            try
            {
                var stale = c.Add(10);

                c.Clear();
                var replacement = c.Add(20);

                Assert.That(c.Has(stale), Is.False);
                Assert.That(c.Has(replacement), Is.True);
                Assert.That(c[replacement], Is.EqualTo(20));
            }
            finally
            {
                c.Dispose();
            }
        }
    }
}
