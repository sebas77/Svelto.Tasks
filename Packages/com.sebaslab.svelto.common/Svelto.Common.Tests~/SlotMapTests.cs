using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class ValueContainerTests
    {
        [Test]
        public void ManagedSlotMap_Count_DecrementsOnRemove_AndReusesFreeSlots()
        {
            var c = new ManagedSlotMap<int>(4);
            try
            {
                var a = c.Add(10);
                var b = c.Add(20);
                var d = c.Add(30);

                Assert.That(c.count, Is.EqualTo(3));
                Assert.That(c.Has(a), Is.True);
                Assert.That(c.Has(b), Is.True);
                Assert.That(c.Has(d), Is.True);

                c.Remove(b);

                Assert.That(c.count, Is.EqualTo(2));
                Assert.That(c.Has(b), Is.False);
                Assert.That(c[a], Is.EqualTo(10));
                Assert.That(c.Has(d), Is.True);

                // Next add should reuse the freed slot (same sparse index) with incremented version.
                var b2 = c.Add(99);

                Assert.That(c.count, Is.EqualTo(3));
                Assert.That(c.Has(b2), Is.True);
                Assert.That(c.Has(b), Is.False);
                Assert.That(c[b2], Is.EqualTo(99));
            }
            finally
            {
                c.Dispose();
            }
        }
    }
}
