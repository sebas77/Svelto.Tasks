using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Svelto.Common;
using Svelto.Utilities;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class UtilityTests
    {
        interface IMarker { }
        sealed class Marker : IMarker { }

        struct InterfaceFieldHolder
        {
            public InterfaceFieldHolder(IMarker value) { this.value = value; }
            public IMarker value;
        }

        struct UnsupportedFieldHolder
        {
            public UnsupportedFieldHolder(int value) { this.value = value; }
            public int value;
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        sealed class MarkerAttribute : Attribute { }

        class ReflectionBase
        {
            [Marker]
            public int BaseValue { get; set; }
        }

        class ReflectionDerived : ReflectionBase
        {
            [Marker]
            public int DerivedValue { get; set; }

            public int ReadOnlyValue => 1;

            [Marker]
            public int MarkedField = 0;
        }

        [Test]
        public void FastConcat_OverloadsConcatenateWithoutRetainingPreviousContents()
        {
            Assert.That("a".FastConcat("b"), Is.EqualTo("ab"));
            Assert.That("n=".FastConcat(-10), Is.EqualTo("n=-10"));
            Assert.That("n=".FastConcat(10u), Is.EqualTo("n=10"));
            Assert.That("n=".FastConcat(10L), Is.EqualTo("n=10"));
            Assert.That("a".FastConcat("b", "c"), Is.EqualTo("abc"));
            Assert.That("a".FastConcat("b", "c", "d"), Is.EqualTo("abcd"));
            Assert.That("a".FastConcat("b", "c", "d", "e"), Is.EqualTo("abcde"));
        }

        [Test]
        public void DataToString_FormatsEmptySingleAndMultipleEntries()
        {
            Assert.That(DataToString.DetailString(new Dictionary<string, string>()), Is.Empty);
            Assert.That(DataToString.DetailString(new Dictionary<string, string> { ["a"] = "1" }),
                Is.EqualTo("<color=teal>\"a\":\"1\"</color>"));
            Assert.That(DataToString.DetailString(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }),
                Is.EqualTo("<color=teal>\"a\":\"1\",</color><color=teal>\"b\":\"2\"</color>"));
        }

        [Test]
        public void FastInvoke_MakeSetterAssignsInterfaceFieldAndRejectsUnsupportedField()
        {
            var interfaceField = typeof(InterfaceFieldHolder).GetField(nameof(InterfaceFieldHolder.value));
            var setter = FastInvoke<InterfaceFieldHolder>.MakeSetter(interfaceField);
            var holder = new InterfaceFieldHolder(null);
            var marker = new Marker();

            setter(ref holder, marker);

            Assert.That(holder.value, Is.SameAs(marker));
            Assert.That(() => FastInvoke<UnsupportedFieldHolder>.MakeSetter(
                typeof(UnsupportedFieldHolder).GetField(nameof(UnsupportedFieldHolder.value))),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Murmur3_VerificationAndSeededHashesAreStable()
        {
            var data = Encoding.UTF8.GetBytes("Svelto");
            var defaultHash = Murmur3.MurmurHash3_x86_32(data);

            Assert.That(Murmur3.VerificationTest(), Is.True);
            Assert.That(Murmur3.MurmurHash3_x86_32(data), Is.EqualTo(defaultHash));
            Assert.That(Murmur3.MurmurHash3_x86_32(data, 42), Is.Not.EqualTo(defaultHash));
        }

        [Test]
        public void StringBuilderUtils_FormatTextNumbersAndTime()
        {
            var builder = new StringBuilder();
            builder.AppendWithColor("text", "red")
                   .AppendWithColor(-12, "blue")
                   .AppendWithColor(long.MinValue, "green")
                   .AppendWithColor(-1.235f, 2, "white")
                   .AppendWithColor(new DateTime(2020, 1, 1, 3, 4, 5), "yellow");

            Assert.That(builder.ToString(), Is.EqualTo(
                "<color=red>text</color><color=blue>-12</color><color=green>-9223372036854775808</color>" +
                "<color=white>-1.24</color><color=yellow>03:04:05</color>"));
        }

        [Test]
        public void TypeAndTimeUtilities_ReturnExpectedValues()
        {
            Assert.That(TypeToString.Name(10), Is.EqualTo(typeof(int).ToString()));
            Assert.That(new TypeToString<string>().Name(), Is.EqualTo(typeof(string).ToString()));
            Assert.That(TimeSpan.FromTicks(123).ToNanoseconds(), Is.EqualTo(12_300));
            Assert.That(Utils.NextPowerOfTwo(0), Is.EqualTo(2));
            Assert.That(Utils.NextPowerOfTwo(3), Is.EqualTo(4));
            Assert.That(Utils.NextPowerOfTwo(9u), Is.EqualTo(16u));
        }

        [Test]
        public void SharedStaticWrapper_SharesDataForSameTypeAndKey()
        {
            var first = new SharedStaticWrapper<int, UtilityTests>();
            var second = new SharedStaticWrapper<int, UtilityTests>();

            first.Data = 42;

            Assert.That(second.Data, Is.EqualTo(42));
        }

        [Test]
        public void NetFxCoreWrappers_ExposeReflectionMetadataAndAttributedWritableProperties()
        {
            Action action = ReflectionTarget;
            var method = action.GetMethodInfoEx();
            var property = typeof(ReflectionDerived).GetProperty(nameof(ReflectionDerived.DerivedValue));
            var field = typeof(ReflectionDerived).GetField(nameof(ReflectionDerived.MarkedField));
            var properties = typeof(ReflectionDerived).FindWritablePropertiesWithCustomAttribute(typeof(MarkerAttribute));
            Action compilerGeneratedAction = () => { };

            Assert.That(method.GetDeclaringType(), Is.EqualTo(typeof(UtilityTests)));
            Assert.That(method, Is.EqualTo(typeof(UtilityTests).GetMethod(nameof(ReflectionTarget), BindingFlags.Static | BindingFlags.NonPublic)));
            Assert.That(typeof(List<int>).GetInterfacesEx(), Does.Contain(typeof(System.Collections.IList)));
            Assert.That(typeof(IDisposable).IsInterfaceEx(), Is.True);
            Assert.That(typeof(int).IsValueTypeEx(), Is.True);
            Assert.That(property.GetDeclaringType(), Is.EqualTo(typeof(ReflectionDerived)));
            Assert.That(typeof(ReflectionDerived).GetBaseType(), Is.EqualTo(typeof(ReflectionBase)));
            Assert.That(property.ContainsCustomAttribute(typeof(MarkerAttribute)), Is.True);
            Assert.That(field.ContainsCustomAttribute(typeof(MarkerAttribute)), Is.True);
            Assert.That(typeof(List<int>).IsGenericTypeEx(), Is.True);
            Assert.That(typeof(List<int>).GetGenericArgumentsEx(), Is.EqualTo(new[] { typeof(int) }));
            Assert.That(properties, Has.Length.EqualTo(2));
            Assert.That(NetFXCoreWrappers.GetCustomAttributes(typeof(UtilityTests), false), Is.Not.Null);
            Assert.That(typeof(UtilityTests).IsCompilerGenerated(), Is.False);
            Assert.That(method.IsCompilerGenerated(), Is.False);
            Assert.That(compilerGeneratedAction.Method.DeclaringType.IsCompilerGenerated(), Is.True);
        }

        static void ReflectionTarget() { }
    }
}
