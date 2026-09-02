using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class TaskSynchronizationContextTests
    {
        [Test]
        public async Task TaskSyncContext_HostedContinuations_RunOnRunnerThread()
        {
            // What we are testing:
            // A hosted async method resumes on the runner's thread: code after an await does not
            // leak onto arbitrary ThreadPool threads.

            var runner = new MultiThreadRunner("SyncCtxControlRunner");

            try
            {
                var mainThreadId    = Thread.CurrentThread.ManagedThreadId;
                var resumedThreadId = 0;
                var done            = false;

                async Task Job()
                {
                    await Task.Yield();

                    resumedThreadId = Thread.CurrentThread.ManagedThreadId;
                    done            = true;
                }

                var context = new TaskSynchronizationContext(runner);

                context.Run(Job);

                var sw = Stopwatch.StartNew();
                while (done == false && sw.ElapsedMilliseconds < 2000)
                    await Task.Delay(10);

                Assert.That(done, Is.True);
                Assert.That(resumedThreadId, Is.Not.EqualTo(mainThreadId),
                    "hosted continuations must resume on the runner thread");
            }
            finally
            {
                runner.Dispose();
            }
        }

        [Test]
        public async Task TaskSyncContext_RunnerDisposed_HostedTaskFreezesForever()
        {
            // What we are testing:
            // Disposing the runner mid-await stops the pump: the hosted task stops being executed
            // immediately and never completes, because queued continuations are never invoked.

            var runner = new MultiThreadRunner("SyncCtxFreezeRunner");

            try
            {
                var iterations = 0;

                async Task Job()
                {
                    while (true)
                    {
                        iterations++;
                        await Task.Yield();
                    }
                }

                var context = new TaskSynchronizationContext(runner);
                var task    = context.Run(Job);

                var sw = Stopwatch.StartNew();
                while (iterations < 5 && sw.ElapsedMilliseconds < 2000)
                    await Task.Delay(10);

                Assert.That(iterations, Is.GreaterThanOrEqualTo(5), "hosted loop must be executing");
                Assert.That(task.IsCompleted, Is.False);

                runner.Dispose(); // pump dies here

                var frozenAt = iterations;

                await Task.Delay(600);

                Assert.That(iterations, Is.EqualTo(frozenAt),
                    "hosted task kept executing after its runner was disposed");
                Assert.That(task.IsCompleted, Is.False,
                    "frozen hosted tasks must never complete");
            }
            finally
            {
                runner.Dispose();
            }
        }

        [Test]
        public void TaskSyncContext_RunnerDisposed_HostedStateMachineBecomesCollectable()
        {
            // What we are testing:
            // Once the runner is disposed and every reference to the returned Task and to the
            // context is dropped, nothing roots the async state machine anymore: it becomes
            // ordinary garbage. The queues of the context root posted continuations, which is why
            // the context reference must be released too.

            var taskRef = HostThenDispose();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.That(taskRef.IsAlive, Is.False,
                "the frozen hosted state machine must be collectable once unreferenced");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference HostThenDispose()
        {
            var runner  = new MultiThreadRunner("SyncCtxGCRunner");
            var context = new TaskSynchronizationContext(runner);

            async Task Job()
            {
                while (true)
                    await Task.Yield();
            }

            var task = context.Run(Job);

            runner.Dispose(); //freeze everything; strong refs die when this frame returns

            return new WeakReference(task);
        }
    }
}
