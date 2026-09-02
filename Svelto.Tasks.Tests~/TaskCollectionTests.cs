using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class TaskCollectionTests
    {
        [Test]
        public void SerialTaskCollection_ExecutesTasksInOrder_AndIsReusable()
        {
            // What we are testing:
            // SerialTaskCollection runs tasks one after another (no overlap) and can be reused after Reset().
            var serial = new SerialTaskCollection("serial");

            var log = new List<int>();

            IEnumerator<TaskContract> TwoPhaseTask(int id)
            {
                log.Add(id); // start
                yield return TaskContract.Yield.It;
                log.Add(-id); // finish
            }

            serial.Add(TwoPhaseTask(1));
            serial.Add(TwoPhaseTask(2));
            serial.Add(TwoPhaseTask(3));

            serial.Complete(1000);

            Assert.That(log, Is.EqualTo(new[] { 1, -1, 2, -2, 3, -3 }));

            serial.Clear();
            log.Clear();

            serial.Add(TwoPhaseTask(4));
            serial.Add(TwoPhaseTask(5));

            using (var runner = new SyncRunner("sync2"))
            {
                serial.RunOn(runner);
                runner.WaitForTasksDoneRelaxed(1000);
            }

            Assert.That(log, Is.EqualTo(new[] { 4, -4, 5, -5 }));
        }

        [Test]
        public void ParallelTaskCollection_AllowsTasksToOverlap()
        {
            // What we are testing:
            // ParallelTaskCollection progresses tasks so that multiple tasks can be in-flight at the same time.

            var parallel = new ParallelTaskCollection("parallel", 4);

            var log = new List<int>();

            IEnumerator<TaskContract> TwoPhaseTask(int id)
            {
                log.Add(id); // start
                yield return TaskContract.Yield.It;
                log.Add(-id); // finish
            }

            parallel.Add(TwoPhaseTask(1));
            parallel.Add(TwoPhaseTask(2));

            // One MoveNext progresses all tasks once. With a parallel collection, both tasks can "start"
            // before either is resumed to "finish".
            parallel.MoveNext();

            Assert.That(log.Count, Is.EqualTo(2));
            Assert.That(log[0] > 0, Is.True);
            Assert.That(log[1] > 0, Is.True);

            parallel.Complete(1000);

            Assert.That(log, Is.EquivalentTo(new[] { 1, 2, -1, -2 }));
            Assert.That(log[0] > 0, Is.True);
            Assert.That(log[1] > 0, Is.True);
        }

#if DEBUG && !DISABLE_DBC && !PROFILE_SVELTO
        [Test]
        public void TaskCollection_AddWhileRunning_Throws()
        {
            // What we are testing:
            // TaskCollection enforces that Add() can't be called while the collection is running.

            var serial = new SerialTaskCollection("serial");

            IEnumerator<TaskContract> YieldOnce()
            {
                yield return TaskContract.Yield.It;
            }

            serial.Add(YieldOnce());

            Assert.Throws<DBC.Tasks.PreconditionException>(() =>
            {
                // One MoveNext puts the collection in running state.
                serial.MoveNext();
                serial.Add(YieldOnce());
            });
        }
#endif

        [Test]
        public void TaskCollection_Clear_RemovesAllTasks()
        {
            // What we are testing:
            // Clear() empties the task collection.

            var serial = new SerialTaskCollection("serial");

            IEnumerator<TaskContract> Empty()
            {
                yield break;
            }

            serial.Add(Empty());
            serial.Add(Empty());

            serial.Clear();

            Assert.That(serial.Current, Is.EqualTo(default(TaskContract)));
        }

        [Test]
        public void SerialTaskCollection_Reset_AllowsReexecution()
        {
            // What we are testing:
            // SerialTaskCollection.Reset() resets the collection and its tasks so they can be run again.
            // Note: The tasks added must support Reset() (e.g. not compiler-generated iterators).

            var serial = new SerialTaskCollection("serial_reset");
            var task = new LeanEnumerator(2); // 2 iterations

            serial.Add(task);

            // Run first time
            serial.Complete(1000);
            Assert.That(task.AllRight, Is.True);

            // Reset
            serial.Reset();
            // LeanEnumerator.Reset() is called by SerialTaskCollection.Reset()
            Assert.That(task.iterations, Is.EqualTo(0));

            // Run second time
            serial.Complete(1000);
            Assert.That(task.AllRight, Is.True);
        }

        [Test]
        public void ParallelTaskCollection_Reset_AllowsReexecution()
        {
            // What we are testing:
            // ParallelTaskCollection.Reset() resets the collection and its tasks so they can be run again.
            // Note: The tasks added must support Reset().

            var parallel = new ParallelTaskCollection("parallel_reset", 2);
            var task1 = new LeanEnumerator(2);
            var task2 = new LeanEnumerator(2);

            parallel.Add(task1);
            parallel.Add(task2);

            // Run first time
            parallel.Complete(1000);
            Assert.That(task1.AllRight, Is.True);
            Assert.That(task2.AllRight, Is.True);

            // Reset
            parallel.Reset();
            Assert.That(task1.iterations, Is.EqualTo(0));
            Assert.That(task2.iterations, Is.EqualTo(0));

            // Run second time
            parallel.Complete(1000);
            Assert.That(task1.AllRight, Is.True);
            Assert.That(task2.AllRight, Is.True);
        }

        // ------------------------------------------------------------------
        // Alignment matrix: collections must behave like the runner wrapper
        // ------------------------------------------------------------------

        public enum CollectionKind { Serial, Parallel }

        static TaskCollection<IEnumerator<TaskContract>> CreateCollection(CollectionKind kind, string name)
        {
            if (kind == CollectionKind.Serial)
                return new SerialTaskCollection(name);

            return new ParallelTaskCollection(name);
        }

        //TaskContract.breakMode is internal: tests observe it through reflection, like ZeroAllocationTests
        //reflect over internal state
        static object GetBreakMode(TaskContract contract)
        {
            return typeof(TaskContract)
                  .GetProperty("breakMode", BindingFlags.NonPublic | BindingFlags.Instance)
                  ?.GetValue(contract);
        }

        [TestCase(CollectionKind.Serial)]
        [TestCase(CollectionKind.Parallel)]
        public void Collection_NestedContinue_ChildRunsToCompletionBeforeParentResumes(CollectionKind kind)
        {
            // What we are testing:
            // A .Continue() child yielded inside a collection runs to completion before the parent resumes.

            var log = new List<string>();

            IEnumerator<TaskContract> Child()
            {
                log.Add("c1");
                yield return TaskContract.Yield.It;
                log.Add("c2");
            }

            IEnumerator<TaskContract> Parent()
            {
                log.Add("p1");
                yield return Child().Continue();
                log.Add("p2");
            }

            var collection = CreateCollection(kind, $"{kind}NestedContinue");
            collection.Add(Parent());

            collection.Complete(1000);

            Assert.That(log, Is.EqualTo(new[] { "p1", "c1", "c2", "p2" }));
        }

        class CountingExtraLeanChild : IEnumerator, IDisposable
        {
            internal CountingExtraLeanChild(List<string> log, string name)
            {
                _log = log;
                _name = name;
            }

            public object Current => null;

            public bool MoveNext()
            {
                _step++;
                _log.Add($"{_name}{_step}");

                return _step < 3;
            }

            public void Reset() { }

            public void Dispose()
            {
                disposeCount++;
            }

            internal int disposeCount;

            readonly List<string> _log;
            readonly string _name;
            int _step;
        }

        [TestCase(CollectionKind.Serial)]
        [TestCase(CollectionKind.Parallel)]
        public void Collection_NestedExtraLeanChild_RunsAcrossTicks_ParentResumesAfter(CollectionKind kind)
        {
            // What we are testing:
            // An ExtraLean child yielded inside a collection is retained across ticks (not abandoned after
            // one step like before the alignment), runs to completion, is disposed exactly once, and only
            // then the parent resumes.

            var log = new List<string>();
            var child = new CountingExtraLeanChild(log, "e");

            IEnumerator<TaskContract> Parent()
            {
                log.Add("p1");
                yield return child.Continue();
                log.Add("p2");
            }

            var collection = CreateCollection(kind, $"{kind}NestedExtraLean");
            collection.Add(Parent());

            collection.Complete(1000);

            Assert.That(log, Is.EqualTo(new[] { "p1", "e1", "e2", "e3", "p2" }));
            Assert.That(child.disposeCount, Is.EqualTo(1), "the completed ExtraLean child must be disposed once");
        }

        [TestCase(CollectionKind.Serial)]
        [TestCase(CollectionKind.Parallel)]
        public void Collection_NestedBreakIt_EndsOnlyTheChild_ParentResumes(CollectionKind kind)
        {
            // What we are testing:
            // A nested Break.It is a soft break: the child is done, the parent resumes, the collection
            // keeps running (it must NOT complete the whole collection like before the alignment).

            var log = new List<string>();

            IEnumerator<TaskContract> Child()
            {
                log.Add("c1");
                yield return TaskContract.Break.It;
                log.Add("cNEVER");
            }

            IEnumerator<TaskContract> Parent()
            {
                log.Add("p1");
                yield return Child().Continue();
                log.Add("p2");
            }

            IEnumerator<TaskContract> Root2()
            {
                log.Add("r2");
                yield break;
            }

            var collection = CreateCollection(kind, $"{kind}NestedBreakIt");
            collection.Add(Parent());
            collection.Add(Root2());

            collection.Complete(1000);

            Assert.That(log, Is.EqualTo(new[] { "p1", "c1", "p2", "r2" }));
        }

        [TestCase(CollectionKind.Serial)]
        [TestCase(CollectionKind.Parallel)]
        public void Collection_RootBreakIt_EndsOnlyThatRoot_CollectionContinues(CollectionKind kind)
        {
            // What we are testing:
            // A root-level Break.It completes only that root task; the remaining roots still run.

            var log = new List<string>();

            IEnumerator<TaskContract> Root1()
            {
                log.Add("r1");
                yield return TaskContract.Break.It;
                log.Add("r1NEVER");
            }

            IEnumerator<TaskContract> Root2()
            {
                log.Add("r2");
                yield break;
            }

            var collection = CreateCollection(kind, $"{kind}RootBreakIt");
            collection.Add(Root1());
            collection.Add(Root2());

            collection.Complete(1000);

            Assert.That(log, Is.EqualTo(new[] { "r1", "r2" }));
        }

        [TestCase(CollectionKind.Serial)]
        [TestCase(CollectionKind.Parallel)]
        public void Collection_BreakAndStop_YieldsStopSignal_AndCancelsRemainingRoots(CollectionKind kind)
        {
            // What we are testing:
            // Break.AndStop anywhere unwinds the whole collection and makes it yield Break.AndStop once
            // (so a runner converts it to StopParentChain); afterwards the collection stays completed.

            var log = new List<string>();

            IEnumerator<TaskContract> HardChild()
            {
                log.Add("c");
                yield return TaskContract.Break.AndStop;
            }

            IEnumerator<TaskContract> Root1()
            {
                log.Add("r1");
                yield return HardChild().Continue();
                log.Add("r1NEVER");
            }

            IEnumerator<TaskContract> Root2()
            {
                log.Add("r2NEVER");
                yield break;
            }

            var collection = CreateCollection(kind, $"{kind}BreakAndStop");
            collection.Add(Root1());
            collection.Add(Root2());

            Assert.That(collection.MoveNext(), Is.True);
            Assert.That(GetBreakMode(collection.Current), Is.EqualTo(TaskContract.Break.AndStop));
            Assert.That(log, Is.EqualTo(new[] { "r1", "c" }));

            Assert.That(collection.MoveNext(), Is.False);
            Assert.That(log, Is.EqualTo(new[] { "r1", "c" }), "remaining roots must stay cancelled");
        }

        [Test]
        public void Collection_RootBreakAndStop_ContinueChainStopsRunnerParent()
        {
            // What we are testing:
            // The full integration through a same-runner .Continue() chain: a parent coroutine waiting on a
            // collection is chain-stopped together with the collection when something inside yields
            // Break.AndStop, while unrelated tasks on the same runner keep completing.

            using (var runner = new SteppableRunner("CollectionChainStop"))
            {
                var log = new List<string>();

                IEnumerator<TaskContract> HardChild()
                {
                    log.Add("c");
                    yield return TaskContract.Break.AndStop;
                }

                IEnumerator<TaskContract> CollectionRoot()
                {
                    log.Add("r1");
                    yield return HardChild().Continue();
                    log.Add("r1NEVER");
                }

                IEnumerator<TaskContract> Unrelated()
                {
                    log.Add("unrelated");
                    yield break;
                }

                var collection = new SerialTaskCollection("chainStopCollection");
                collection.Add(CollectionRoot());

                IEnumerator<TaskContract> Parent()
                {
                    log.Add("before");
                    yield return collection.Continue();
                    log.Add("afterNEVER");
                }

                Parent().RunOn(runner);
                Unrelated().RunOn(runner);

                Assert.That(runner.WaitForTasksDone(16, 2000), Is.True);

                Assert.That(log, Is.EqualTo(new[] { "before", "r1", "c", "unrelated" }),
                    "the waiting parent must be chain-stopped, unrelated tasks must complete");
            }
        }

        [Test]
        public void Collection_RootBreakAndStop_RunOnParentResumesByDesign()
        {
            // What we are testing:
            // A .RunOn() task belongs to a separate runner path by design (like every RunOn task, a
            // collection run through RunOn is a root on its own): Break.AndStop inside the collection
            // cancels the collection itself, but a RunOn-waiting parent simply resumes on completion.

            using (var runner = new SteppableRunner("CollectionRunOnResume"))
            {
                var log = new List<string>();

                IEnumerator<TaskContract> HardChild()
                {
                    log.Add("c");
                    yield return TaskContract.Break.AndStop;
                }

                IEnumerator<TaskContract> CollectionRoot()
                {
                    log.Add("r1");
                    yield return HardChild().Continue();
                    log.Add("r1NEVER");
                }

                var collection = new SerialTaskCollection("runOnResumeCollection");
                collection.Add(CollectionRoot());

                IEnumerator<TaskContract> Parent()
                {
                    log.Add("before");
                    yield return collection.RunOn(runner);
                    log.Add("after");
                }

                Parent().RunOn(runner);

                Assert.That(runner.WaitForTasksDone(16, 2000), Is.True);

                Assert.That(log, Is.EqualTo(new[] { "before", "r1", "c", "after" }),
                    "RunOn parents are not part of the collection chain, they resume by design");
            }
        }

        [TestCase(CollectionKind.Serial)]
        [TestCase(CollectionKind.Parallel)]
        public void Collection_ValueYield_CompletesTheEnumerator(CollectionKind kind)
        {
            // What we are testing:
            // A value-yield completes the enumerator that returned it (a value completes a Lean task too),
            // and the collection continues with the remaining work.

            var log = new List<string>();

            IEnumerator<TaskContract> Root1()
            {
                log.Add("r1");
                yield return 42;
                log.Add("r1NEVER");
            }

            IEnumerator<TaskContract> Root2()
            {
                log.Add("r2");
                yield break;
            }

            var collection = CreateCollection(kind, $"{kind}ValueYield");
            collection.Add(Root1());
            collection.Add(Root2());

            collection.Complete(1000);

            Assert.That(log, Is.EqualTo(new[] { "r1", "r2" }));
        }

        [Test]
        public void Collection_Forget_Throws()
        {
            // What we are testing:
            // .Forget() inside a collection fails fast: a collection cannot schedule independent work.

            var log = new List<string>();

            IEnumerator<TaskContract> Child()
            {
                yield break;
            }

            IEnumerator<TaskContract> Parent()
            {
                log.Add("p1");
                yield return Child().Forget();
            }

            var collection = new SerialTaskCollection("forgetThrows");
            collection.Add(Parent());

            Assert.That(() => collection.MoveNext(), Throws.TypeOf<SveltoTaskException>());
            Assert.That(log, Is.EqualTo(new[] { "p1" }));
        }
    }
}
