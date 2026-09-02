using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.ExtraLean;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class SyncRunnerTests
    {
        [Test]
        public void Lean_SyncRunner_RunsTaskSynchronously()
        {
            bool executed = false;
            IEnumerator<TaskContract> Task()
            {
                executed = true;
                yield break;
            }

            using (var runner = new Lean.SyncRunner("LeanSyncRunner"))
            {
                Task().RunOn(runner);
                runner.WaitForTasksDoneRelaxed(1000);
            }

            Assert.That(executed, Is.True);
        }

        [Test]
        public void ExtraLean_SyncRunner_RunsTaskSynchronously()
        {
            bool executed = false;
            IEnumerator Task()
            {
                executed = true;
                yield break;
            }

            using (var runner = new ExtraLean.SyncRunner("ExtraLeanSyncRunner"))
            {
                Task().RunOn(runner);
                runner.WaitForTasksDoneRelaxed(1000);
            }

            Assert.That(executed, Is.True);
        }

        [Test]
        public void Lean_SyncRunner_ExecutesNestedTasks()
        {
            int count = 0;
            IEnumerator<TaskContract> Task()
            {
                count++;
                yield return TaskContract.Yield.It;
                count++;
            }

            using (var runner = new Lean.SyncRunner("LeanSyncRunner"))
            {
                Task().RunOn(runner);
                runner.WaitForTasksDoneRelaxed(1000);
            }

            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void ExtraLean_SyncRunner_ExecutesNestedTasks()
        {
            int count = 0;
            IEnumerator Task()
            {
                count++;
                yield return null;
                count++;
            }

            using (var runner = new ExtraLean.SyncRunner("ExtraLeanSyncRunner"))
            {
                Task().RunOn(runner);
                runner.WaitForTasksDoneRelaxed(1000);
            }

            Assert.That(count, Is.EqualTo(2));
        }
    }
}
