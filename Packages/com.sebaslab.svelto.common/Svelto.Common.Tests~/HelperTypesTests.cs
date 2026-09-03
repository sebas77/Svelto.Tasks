using System;
using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class HelperTypesTests
    {
        struct CaseInsensitiveComparer : IEqualityComparer<string>
        {
            public bool Equals(string left, string right) => StringComparer.OrdinalIgnoreCase.Equals(left, right);
            public int GetHashCode(string value) => StringComparer.OrdinalIgnoreCase.GetHashCode(value);
        }

        [Test]
        public void RefWrapper_EqualityHashAndConversionsUseWrappedReference()
        {
            string value = new string(new[] { 'v', 'a', 'l', 'u', 'e' });
            RefWrapper<string> wrapper = value;
            string converted = wrapper;

            Assert.That(wrapper.Equals(value), Is.True);
            Assert.That(wrapper.Equals(new RefWrapper<string>(value)), Is.True);
            Assert.That(wrapper.GetHashCode(), Is.EqualTo(value.GetHashCode()));
            Assert.That(converted, Is.SameAs(value));
            Assert.That(wrapper.type, Is.SameAs(value));
        }

        [Test]
        public void RefWrapper_WithComparerUsesComparerForEqualityAndHashing()
        {
            var upper = new RefWrapper<string, CaseInsensitiveComparer>("VALUE");
            var lower = new RefWrapper<string, CaseInsensitiveComparer>("value");

            Assert.That(upper.Equals(lower), Is.True);
            Assert.That(upper.Equals("value"), Is.True);
            Assert.That(upper.GetHashCode(), Is.EqualTo(lower.GetHashCode()));
        }

        [Test]
        public void RefWrapperTypeVariants_PreserveIdentityAndConversions()
        {
            var intType = new RefWrapperType(typeof(int));
            var otherIntType = new RefWrapperType(typeof(int));
            var stringType = new RefWrapperType(typeof(string));
            var nativeIntType = new NativeRefWrapperType(intType);
            var otherNativeIntType = new NativeRefWrapperType(otherIntType);
            RefWrapperString text = "value";

            Assert.That(intType.Equals(otherIntType), Is.True);
            Assert.That(intType.Equals(stringType), Is.False);
            Assert.That((Type)intType, Is.EqualTo(typeof(int)));
            Assert.That(nativeIntType.Equals(otherNativeIntType), Is.True);
            Assert.That(nativeIntType.GetHashCode(), Is.EqualTo(intType.GetHashCode()));
            Assert.That(text.Equals((RefWrapperString)"value"), Is.True);
            Assert.That((string)text, Is.EqualTo("value"));
            Assert.That(TypeRefWrapper<int>.wrapper.Equals(intType), Is.True);
        }

        [Test]
        public void TypeCacheAndTypeExtensions_ReportRuntimeTypeProperties()
        {
            var integer = 10;
            var text = "value";

            Assert.That(TypeCache<int>.type, Is.EqualTo(typeof(int)));
            Assert.That(TypeCache<int>.name, Is.EqualTo(nameof(Int32)));
            Assert.That(TypeCache<int>.fullName, Is.EqualTo(typeof(int).FullName));
            Assert.That(integer.isUnmanaged(), Is.True);
            Assert.That(text.isUnmanaged(), Is.False);
            Assert.That(text.name(), Is.EqualTo(nameof(String)));
            Assert.That(TypeHash<int>.hash, Is.EqualTo(typeof(int).GetHashCode()));
        }

        [Test]
        public void HashHelpers_FastModMatchesRemainder()
        {
            const uint divisor = 37;
            var multiplier = HashHelpers.GetFastModMultiplier(divisor);

            foreach (var value in new uint[] { 0, 1, 36, 37, 38, 1000, uint.MaxValue })
                Assert.That(HashHelpers.FastMod(value, divisor, multiplier), Is.EqualTo(value % divisor));
        }

        [Test]
        public void HashHelpers_GetPrimeAndExpandRespectTableBoundaries()
        {
            Assert.That(HashHelpers.GetPrime(0), Is.EqualTo(1));
            Assert.That(HashHelpers.GetPrime(24), Is.EqualTo(29));
            Assert.That(HashHelpers.GetPrime(14591), Is.EqualTo(14591));
            Assert.That(HashHelpers.GetPrime(14592), Is.EqualTo(17519));
            Assert.That(HashHelpers.Expand(3), Is.EqualTo(7));
            Assert.That(HashHelpers.Expand(7_199_369), Is.EqualTo(10_799_054));
            Assert.That(() => HashHelpers.GetPrime(7_199_370), Throws.TypeOf<ArgumentException>());
        }
    }
}
