using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class StoppedRunnerNetTaskTests
    {
        [Test]
        public async Task MultiThreadRunner_DisposeMidFlight_BridgedNetTaskNeverCompletes()
        {
            // What we are testing:
            // A .NET Task bridged onto a Svelto coroutine running on a MultiThreadRunner must NEVER
            // complete when the runner is explicitly stopped mid-flight. The coroutine never reaches
            // its last statement, so the completion flag stays false forever. Note that we cannot use
            // continuation.isRunning for the bridge: disposing a task recycles its continuation, which
            // flips isRunning to false even though the work never finished.

            var finished = false;

            IEnumerator<TaskContract> LongJob()
            {
                for (var i = 0; i < 1000000; i++)
                    yield return TaskContract.Yield.It;

                finished = true;
            }

            async Task BridgeToNetTask()
            {
                while (finished == false)
                    await Task.Yield();
            }

            var runner = new MultiThreadRunner("StopNetTaskRunner");

            try
            {
                LongJob().RunOn(runner);

                var netTask = BridgeToNetTask();

                Thread.Sleep(100); // let the background thread actually start the job

                Assert.That(runner.hasTasks, Is.True);
                Assert.That(finished, Is.False);

                runner.Dispose(); // explicit stop: thread exits, tasks abandoned

                var winner = await Task.WhenAny(netTask, Task.Delay(1000));

                Assert.That(winner, Is.Not.EqualTo(netTask),
                    "bridged .NET Task completed even though the runner was disposed mid-flight");
                Assert.That(netTask.IsCompleted, Is.False);
                Assert.That(finished, Is.False);
            }
            finally
            {
                runner.Dispose();
            }
        }

        [Test]
        public async Task MultiThreadRunner_LeftAlive_BridgedNetTaskCompletes()
        {
            // What we are testing:
            // Control test for the bridge pattern: with a runner left alive, the same body-flag bridge
            // completes as soon as the coroutine runs to its final statement.

            var finished = false;

            IEnumerator<TaskContract> QuickJob()
            {
                for (var i = 0; i < 3; i++)
                    yield return TaskContract.Yield.It;

                finished = true;
            }

            async Task BridgeToNetTask()
            {
                while (finished == false)
                    await Task.Yield();
            }

            using (var runner = new SteppableRunner("BridgeControlRunner"))
            {
                QuickJob().RunOn(runner);

                var netTask = BridgeToNetTask();

                var done = runner.WaitForTasksDone(2000);

                var winner = await Task.WhenAny(netTask, Task.Delay(1000));

                Assert.That(done, Is.True);
                Assert.That(winner, Is.EqualTo(netTask));
                Assert.That(netTask.IsCompleted, Is.True);
                Assert.That(finished, Is.True);
            }
        }
    }
}
