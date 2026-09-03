using System;
using Svelto.Context;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public class WeakAndContextTests
    {
        sealed class Listener : IWaitForFrameworkInitialization, IWaitForFrameworkDestruction
        {
            readonly string _name;
            readonly FasterList<string> _notifications;

            public Listener(string name, FasterList<string> notifications)
            {
                _name = name;
                _notifications = notifications;
            }

            public int value;
            public void Increment() => value++;
            public void Add(int amount) => value += amount;
            public void OnFrameworkInitialized() => _notifications.Add("init:" + _name);
            public void OnFrameworkDestroyed() => _notifications.Add("destroy:" + _name);
        }

        static int _staticValue;
        static void IncrementStatic() => _staticValue++;
        static void AddStatic(int amount) => _staticValue += amount;

        [SetUp]
        public void SetUp() => _staticValue = 0;

        [Test]
        public void WeakReference_TracksTargetEqualityAndDispose()
        {
            var target = new object();
            var weak = new DataStructures.WeakReference<object>(target);

            Assert.That(weak.IsValid, Is.True);
            Assert.That(weak.IsAlive, Is.True);
            Assert.That(weak.Target, Is.SameAs(target));
            Assert.That(weak.TryGetTarget(out var found), Is.True);
            Assert.That(found, Is.SameAs(target));
            Assert.That(weak.Equals(target), Is.True);
            Assert.That(weak.GetHashCode(target), Is.EqualTo(target.GetHashCode()));

            weak.Dispose();

            Assert.That(weak.IsValid, Is.False);
            Assert.That(weak.TryGetTarget(out found), Is.False);
            Assert.That(found, Is.Null);
        }

        [Test]
        public void WeakAction_StaticAndInstanceActionsInvoke()
        {
            var listener = new Listener("listener", new FasterList<string>());
            var instance = new WeakAction(listener.Increment);
            var genericInstance = new WeakAction<int>(listener.Add);
            var staticAction = new WeakAction(IncrementStatic);
            var genericStatic = new WeakAction<int>(AddStatic);

            instance.Invoke();
            genericInstance.Invoke(3);
            staticAction.Invoke();
            genericStatic.Invoke(4);

            Assert.That(instance.IsAlive, Is.True);
            Assert.That(genericInstance.IsAlive, Is.True);
            Assert.That(staticAction.IsAlive, Is.True);
            Assert.That(genericStatic.IsAlive, Is.True);
            Assert.That(listener.value, Is.EqualTo(4));
            Assert.That(_staticValue, Is.EqualTo(5));
        }

        [Test]
        public void WeakAction_RejectsNullActions()
        {
            Assert.That(() => new WeakAction(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new WeakAction<int>(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void WeakEvent_AddRemoveAndInvokeWorkForBothVariants()
        {
            WeakEvent noArguments = null;
            noArguments += IncrementStatic;
            noArguments += IncrementStatic;
            noArguments -= IncrementStatic;
            noArguments.Invoke();

            WeakEvent<int> withArgument = null;
            withArgument += AddStatic;
            withArgument.Invoke(4);
            withArgument -= AddStatic;
            withArgument.Invoke(100);

            Assert.That(_staticValue, Is.EqualTo(5));
            Assert.That(noArguments - null, Is.SameAs(noArguments));
            Assert.That((WeakEvent)null - IncrementStatic, Is.Null);
            Assert.That(withArgument - null, Is.SameAs(withArgument));
            Assert.That((WeakEvent<int>)null - AddStatic, Is.Null);
        }

        [Test]
        public void ContextNotifier_NotifiesInReverseRegistrationOrderAndOnlyOnce()
        {
            var notifications = new FasterList<string>();
            var first = new Listener("first", notifications);
            var second = new Listener("second", notifications);
            var notifier = new ContextNotifier();
            notifier.AddFrameworkInitializationListener(first);
            notifier.AddFrameworkInitializationListener(second);
            notifier.AddFrameworkDestructionListener(first);
            notifier.AddFrameworkDestructionListener(second);

            notifier.NotifyFrameworkInitialized();
            notifier.NotifyFrameworkDeinitialized();

            Assert.That(notifications.ToArray(), Is.EqualTo(new[]
            {
                "init:second", "init:first", "destroy:second", "destroy:first"
            }));
            Assert.That(() => notifier.AddFrameworkInitializationListener(first), Throws.Exception);
            Assert.That(() => notifier.AddFrameworkDestructionListener(first), Throws.Exception);
        }
    }
}
