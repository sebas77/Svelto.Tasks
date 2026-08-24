namespace Svelto.Common.Tests
{
    [TestFixture]
    public class MemoryUtilitiesAlignmentTests
    {
        [TestCase(0u, 0u)]
        [TestCase(1u, 4u)]
        [TestCase(2u, 4u)]
        [TestCase(3u, 4u)]
        [TestCase(4u, 4u)]
        [TestCase(5u, 8u)]
        [TestCase(7u, 8u)]
        [TestCase(8u, 8u)]
        [TestCase(15u, 16u)]
        [TestCase(16u, 16u)]
        [TestCase(17u, 20u)]
        public void Align4_ReturnsNextMultipleOf4(uint input, uint expected)
        {
            Assert.That(MemoryUtilities.Align4(input), Is.EqualTo(expected));
        }

        [TestCase(0u, 0u)]
        [TestCase(1u, 3u)]
        [TestCase(2u, 2u)]
        [TestCase(3u, 1u)]
        [TestCase(4u, 0u)]
        [TestCase(5u, 3u)]
        [TestCase(7u, 1u)]
        [TestCase(8u, 0u)]
        public void Pad4_ReturnsPaddingToReachNextMultipleOf4(uint input, uint expectedPadding)
        {
            var pad = MemoryUtilities.Pad4(input);

            Assert.That(pad, Is.EqualTo(expectedPadding));
            Assert.That(((input + pad) & 3u) == 0u, Is.True);
        }

        [Test]
        public void Pad4_NeverReturnsValueGreaterThan3()
        {
            Assert.That(MemoryUtilities.Pad4(0u) <= 3u, Is.True);
            Assert.That(MemoryUtilities.Pad4(1u) <= 3u, Is.True);
            Assert.That(MemoryUtilities.Pad4(2u) <= 3u, Is.True);
            Assert.That(MemoryUtilities.Pad4(3u) <= 3u, Is.True);
            Assert.That(MemoryUtilities.Pad4(4u) <= 3u, Is.True);
            Assert.That(MemoryUtilities.Pad4(uint.MaxValue) <= 3u, Is.True);
        }

        [Test]
        public void Align4_ResultIsAlwaysMultipleOf4_ForSafeRangeInputs()
        {
            // Keep inputs far enough from uint overflow when aligning.
            var inputs = new[] { 0u, 1u, 2u, 3u, 4u, 5u, 123u, 1024u, uint.MaxValue - 16u };

            for (var i = 0; i < inputs.Length; i++)
            {
                var aligned = MemoryUtilities.Align4(inputs[i]);

                Assert.That((aligned & 3u) == 0u, Is.True);
                Assert.That(aligned >= inputs[i], Is.True);
            }
        }
    }
}
