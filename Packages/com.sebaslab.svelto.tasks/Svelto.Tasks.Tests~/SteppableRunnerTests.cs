using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.ExtraLean;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class SteppableRunnerTests
    {
        [Test]
        public void Lean_SteppableRunner_StepsThroughTask()
        {
            int count = 0;
            IEnumerator<TaskContract> Task()
            {
                count++;
                yield return TaskContract.Yield.It;
                count++;
            }

            using (var runner = new Lean.SteppableRunner("LeanSteppableRunner"))
            {
                Task().RunOn(runner);
                
                Assert.That(count, Is.EqualTo(0));
                
                runner.Step();
                Assert.That(count, Is.EqualTo(1));
                
                runner.Step();
                Assert.That(count, Is.EqualTo(2));
            }
        }

        [Test]
        public void ExtraLean_SteppableRunner_StepsThroughTask()
        {
            int count = 0;
            IEnumerator Task()
            {
                count++;
                yield return null;
                count++;
            }

            using (var runner = new ExtraLean.SteppableRunner("ExtraLeanSteppableRunner"))
            {
                Task().RunOn(runner);
                
                Assert.That(count, Is.EqualTo(0));
                
                runner.Step();
                Assert.That(count, Is.EqualTo(1));
                
                runner.Step();
                Assert.That(count, Is.EqualTo(2));
            }
        }
    }
}

