using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class SveltoDictionaryRecycleOrAddTests
    {
        abstract class BaseValue
        {
            public int value;
        }

        sealed class Box : BaseValue
        {
            public Box(int value) { this.value = value; }
        }

        [Test]
        public void RecycleOrAdd_WhenKeyMissing_BuildsValueAndReturnsIt()
        {
            var dic = new SveltoDictionary<int, BaseValue, ManagedStrategy<SveltoDictionaryNode<int>>, ManagedStrategy<BaseValue>, ManagedStrategy<int>>(1, Allocator.Managed);

            var builderCalls = 0;
            var recyclerCalls = 0;

            ref BaseValue v = ref dic.RecycleOrAdd<Box>(
                1,
                () =>
                {
                    builderCalls++;
                    return new Box(123);
                },
                (ref Box boxed) =>
                {
                    recyclerCalls++;
                    boxed.value = 999;
                });

            Assert.That(builderCalls, Is.EqualTo(1));
            Assert.That(recyclerCalls, Is.EqualTo(0));
            Assert.That(v, Is.Not.Null);
            Assert.That(((Box)v).value, Is.EqualTo(123));
        }

        [Test]
        public void RecycleOrAdd_WhenKeyExists_ReturnsExistingValue_AndDoesNotCallBuilderOrRecycler()
        {
            var dic = new SveltoDictionary<int, BaseValue, ManagedStrategy<SveltoDictionaryNode<int>>, ManagedStrategy<BaseValue>, ManagedStrategy<int>>(1, Allocator.Managed);

            var first = new Box(10);
            dic.Add(1, first);

            var builderCalls = 0;
            var recyclerCalls = 0;

            ref BaseValue v = ref dic.RecycleOrAdd<Box>(
                1,
                () =>
                {
                    builderCalls++;
                    return new Box(123);
                },
                (ref Box boxed) =>
                {
                    recyclerCalls++;
                    boxed.value = 999;
                });

            Assert.That(builderCalls, Is.EqualTo(0));
            Assert.That(recyclerCalls, Is.EqualTo(0));
            Assert.That(object.ReferenceEquals(v, first), Is.True);
        }
    }
}
