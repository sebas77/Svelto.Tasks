using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Lean;
using Svelto.Tasks.ExtraLean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class TaskContractTests
    {
        [Test]
        public void TaskContract_Int_StoresAndRetrievesValue()
        {
            TaskContract contract = new TaskContract(123);
            Assert.That(contract.ToInt(), Is.EqualTo(123));
        }

        [Test]
        public void TaskContract_Ulong_StoresAndRetrievesValue()
        {
            TaskContract contract = new TaskContract(1234567890123456789UL);
            Assert.That(contract.ToUlong(), Is.EqualTo(1234567890123456789UL));
        }

        [Test]
        public void TaskContract_Float_StoresAndRetrievesValue()
        {
            TaskContract contract = new TaskContract(123.456f);
            Assert.That(contract.ToFloat(), Is.EqualTo(123.456f));
        }

        [Test]
        public void TaskContract_Uint_StoresAndRetrievesValue()
        {
            TaskContract contract = new TaskContract(1234567890U);
            Assert.That(contract.ToUInt(), Is.EqualTo(1234567890U));
        }

        [Test]
        public void TaskContract_Bool_StoresAndRetrievesValue()
        {
            TaskContract contract = new TaskContract(true);
            Assert.That(contract.ToBool(), Is.True);
        }

        [Test]
        public void TaskContract_String_StoresAndRetrievesValue()
        {
            TaskContract contract = new TaskContract("test string");
            Assert.That(contract.ToRef<string>(), Is.EqualTo("test string"));
        }

        [Test]
        public void TaskContract_Exception_StoresAndRetrievesValue()
        {
            var ex = new Exception("test exception");
            TaskContract contract = new TaskContract(ex);
            Assert.That(contract.ToRef<Exception>(), Is.SameAs(ex));
        }

        [Test]
        public void TaskContract_FromReference_StoresAndRetrievesValue()
        {
            var obj = new object();
            TaskContract contract = TaskContract.FromReference(obj);
            Assert.That(contract.ToRef<object>(), Is.SameAs(obj));
        }

        [Test]
        public void TaskContractContinueIt_TriggersImmediateMoveNext()
        {
            TaskContract contract = TaskContract.Continue.It;
            Assert.That(contract.continueIt, Is.True);
        }

        [Test]
        public void Forget_RunsChildButParentDoesNotWait()
        {
            using (var runner = new Lean.SteppableRunner("Forget"))
            {
                var order = new List<int>();

                IEnumerator<TaskContract> Child()
                {
                    order.Add(2);
                    yield return TaskContract.Yield.It;
                    order.Add(3);
                }

                IEnumerator<TaskContract> Parent()
                {
                    order.Add(1);
                    yield return Child().Forget();
                    order.Add(4);
                }

                Parent().RunOn(runner);

                runner.Step();

                Assert.That(order.Count, Is.EqualTo(1));
                Assert.That(order[0], Is.EqualTo(1));

                runner.Step();
                Assert.That(order, Is.EqualTo(new[] { 1, 4, 2 }));

                runner.Step();
                Assert.That(order, Is.EqualTo(new[] { 1, 4, 2, 3 }));
            }
        }

        [Test]
        public void ExtraLean_InvalidCurrent_ThrowsSveltoTaskException()
        {
            using (var runner = new ExtraLean.SteppableRunner("ExtraLean_Invalid"))
            {
                IEnumerator Invalid()
                {
                    yield return 123;
                }

                Invalid().RunOn(runner);

                Exception caughtException = null;

                void OnException(Exception ex, string msg)
                {
                    caughtException = ex;
                }

                Console.onException += OnException;

                try
                {
                    runner.Step();
                }
                finally
                {
                    Console.onException -= OnException;
                }
                
                Assert.That(caughtException, Is.Not.Null);
                Assert.That(caughtException.GetType().Name, Is.EqualTo("SveltoTaskException"));
            }
        }
    }
}

