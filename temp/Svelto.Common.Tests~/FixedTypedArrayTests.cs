#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER
namespace Svelto.Common.Tests
{
    [TestFixture]
    public class FixedTypedArrayTests
    {
        [Test]
        public void FixedTypedArray4_Indexer_ReadWrite_Works()
        {
            var a = new FixedTypedArray4<int>();

            Assert.That(a.capacity, Is.EqualTo(4));

            a[0] = 10;
            a[1] = 11;
            a[2] = 12;
            a[3] = 13;

            Assert.That(a[0], Is.EqualTo(10));
            Assert.That(a[1], Is.EqualTo(11));
            Assert.That(a[2], Is.EqualTo(12));
            Assert.That(a[3], Is.EqualTo(13));
        }

        [Test]
        public void FixedTypedArray8_Indexer_ReadWrite_Works()
        {
            var a = new FixedTypedArray8<int>();

            Assert.That(a.capacity, Is.EqualTo(8));

            for (var i = 0; i < a.capacity; i++)
                a[i] = i * 2;

            for (var i = 0; i < a.capacity; i++)
                Assert.That(a[i], Is.EqualTo(i * 2));
        }

        [Test]
        public void FixedTypedArray16_Indexer_ReadWrite_Works()
        {
            var a = new FixedTypedArray16<int>();

            Assert.That(a.capacity, Is.EqualTo(16));

            for (var i = 0; i < a.capacity; i++)
                a[i] = i + 100;

            for (var i = 0; i < a.capacity; i++)
                Assert.That(a[i], Is.EqualTo(i + 100));
        }

        [Test]
        public void FixedTypedArray32_Indexer_ReadWrite_Works()
        {
            var a = new FixedTypedArray32<int>();

            Assert.That(a.capacity, Is.EqualTo(32));

            for (var i = 0; i < a.capacity; i++)
                a[i] = i;

            for (var i = 0; i < a.capacity; i++)
                Assert.That(a[i], Is.EqualTo(i));
        }
    }
}
#endif
