using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class SequenceTests
    {
        interface IItem { }

        [Sequenced("first")]
        sealed class First : IItem { }

        [Sequenced("second")]
        sealed class Second : IItem { }

        [Sequenced("unknown")]
        sealed class Unknown : IItem { }

        sealed class Untagged : IItem { }

        struct ValidOrder : ISequenceOrder
        {
            public string[] enginesOrder => new[] { "first", "second" };
        }

        struct DuplicateOrder : ISequenceOrder
        {
            public string[] enginesOrder => new[] { "first", "first" };
        }

        struct NullOrder : ISequenceOrder
        {
            public string[] enginesOrder => null;
        }

        [Test]
        public void Sequence_SortsByAttributeAndRemoveClearsRequestedSlot()
        {
            var items = new FasterList<IItem>();
            var second = new Second();
            var first = new First();
            items.Add(second);
            items.Add(first);

            var sequence = new Sequence<IItem, ValidOrder>(new FasterReadOnlyList<IItem>(items));

            Assert.That(sequence.items[0], Is.SameAs(first));
            Assert.That(sequence.items[1], Is.SameAs(second));

            sequence.Remove(0);
            sequence.Remove(-1);
            sequence.Remove(10);

            Assert.That(sequence.items[0], Is.Null);
            Assert.That(sequence.items[1], Is.SameAs(second));
        }

        [Test]
        public void Sequence_RejectsUntaggedUnknownAndDuplicateItems()
        {
            Assert.That(() => CreateSequence(new Untagged()), Throws.Exception);
            Assert.That(() => CreateSequence(new Unknown()), Throws.Exception);
#if DEBUG
            Assert.That(() => CreateSequence(new First(), new First()), Throws.Exception);
#else
            Assert.That(() => CreateSequence(new First(), new First()), Throws.Nothing);
#endif
        }

        [Test]
        public void SequenceOrder_RejectsNullAndDuplicateDefinitions()
        {
            var empty = new FasterList<IItem>();

            Assert.That(() => new Sequence<IItem, NullOrder>(new FasterReadOnlyList<IItem>(empty)), Throws.Exception);
            Assert.That(() => new Sequence<IItem, DuplicateOrder>(new FasterReadOnlyList<IItem>(empty)), Throws.Exception);
        }

#if DEBUG
        [Test]
        public void Sequence_DebugBuildRequiresEveryDefinedItem()
        {
            Assert.That(() => CreateSequence(new First()), Throws.Exception);
        }
#endif

        static Sequence<IItem, ValidOrder> CreateSequence(params IItem[] values)
        {
            var items = new FasterList<IItem>();
            foreach (var value in values)
                items.Add(value);

            return new Sequence<IItem, ValidOrder>(new FasterReadOnlyList<IItem>(items));
        }
    }
}
