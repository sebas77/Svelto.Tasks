using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.ExtraLean;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;
using Svelto.Tasks.Parallelism;

namespace Svelto.Tasks.Tests
{
    /// <summary>
    /// Proves that Svelto.Tasks runs without triggering a single heap allocation when runners are
    /// preallocated for the expected number of concurrent tasks.
    ///
    /// Requirements for these tests to be meaningful:
    /// - Svelto.Tasks MUST be built in Release. The DEBUG keyword enables by-design diagnostic
    ///   allocations (WeakReference in every Continuation, DBC check strings, ExtraLean name
    ///   generation), so the fixture detects a debug/profiling build and skips itself.
    /// - Runners must be constructed with enough initialNumberOfTasks capacity so their internal
    ///   containers never grow during the measured waves.
    /// - Every workload is warmed up before measuring so JIT, static constructors, container growth
    ///   and one-off caches (ConcurrentQueue segments, ContinuationPool, ThreadLocal sync runners)
    ///   reach their steady state outside of the measured window.
/// - Measurements use GC.GetAllocatedBytesForCurrentThread. Background-runner threads are
///   measured from within tasks executed by those very threads: a start marker samples the
///   counter on its first MoveNext and an end marker samples it on its first MoveNext AFTER
///   the last real task fully completed, so the window includes the final task's cleanup.
    /// </summary>
    [TestFixture]
    public class ZeroAllocationTests
    {
        const int ConcurrentTasks = 16;
        const int StepsPerTask = 8;
        const int Waves = 50;
        const int MtWaves = 25;
        const int IdleSteps = 1000;
        const int ChildTasks = 8;
        const int JobIterations = 32;
        const uint RunnerCapacity = 64;
        const uint CollectionThreads = 4;
        const int MtTimeoutMs = 5000;
        //Measure() executes the workload 2 warmup times + 1 measured time. Counters accumulate across
        //all three executions, so completion assertions must demand three runs' worth of work: a
        //single-run expectation would already be satisfied by the warmups and would not prove that the
        //measured invocation actually executed any task.
        const int MeasureTotalExecutions = 3;

        [OneTimeSetUp]
        public void SkipUnlessLibraryIsReleaseBuild()
        {
            if (HasDebugOnlyContinuationField())
                Assert.Ignore(
                    "Svelto.Tasks is compiled with DEBUG: diagnostic allocations (WeakReference in " +
                    "Continuation, DBC strings) are enabled by design. Zero-allocation guarantees can " +
                    "only be verified against a Release build: dotnet test -c Release");

            if (HasGeneratedTaskNames())
                Assert.Ignore(
                    "Svelto.Tasks is compiled with name generation enabled (DEBUG or profiler defines): " +
                    "task names are allocated by design. Run with: dotnet test -c Release");
        }

        static bool HasDebugOnlyContinuationField()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

            return typeof(Svelto.Tasks.Enumerators.Continuation).GetField("_runner", flags) != null;
        }

        static bool HasGeneratedTaskNames()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

            return typeof(ExtraLean.Struct.ExtraLeanSveltoTask<CountingExtraLeanStructTask>)
                  .GetField("_name", flags) != null;
        }

        static long Measure(Action workload, int warmupRuns = 2)
        {
            for (int i = 0; i < warmupRuns; i++)
                workload();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            long before = GC.GetAllocatedBytesForCurrentThread();
            workload();

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        static void AssertZero(long bytes, string context)
        {
            Assert.That(bytes, Is.EqualTo(0),
                $"{context}: {bytes} bytes were allocated on the executing thread. Ensure Svelto.Tasks " +
                "is built in Release (dotnet test -c Release), the runner is preallocated for the number " +
                "of concurrent tasks, and no new code path was introduced that allocates.");
        }

        [Test]
        public void Lean_SteppableRunner_SubmitStepComplete_IsZeroAllocation()
        {
            using (var runner = new Lean.SteppableRunner("zeroalloc-lean-stepper", RunnerCapacity))
            {
                var tasks = CreateLeanTasks();

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        SubmitAll(runner, tasks);

                        while (runner.hasTasks)
                            runner.Step();
                    }
                });

                AssertZero(allocated, $"Lean SteppableRunner {Waves} waves x {ConcurrentTasks} tasks");
                AssertAllCompleted(tasks, Waves * MeasureTotalExecutions);
            }
        }

        [Test]
        public void Lean_GenericSteppableRunner_StructTasks_AreZeroAllocation()
        {
            //struct tasks on the generic runner must never box: the struct-typed RunOn overload stores
            //the task by value and the wrapper steps it by reference through the TombstoneList slot
            //(SveltoTaskWrapper._task field). This is the zero-allocation counterpart of Example 04.
            using (var runner = new Lean.SteppableRunner<CountingStructTask>("zeroalloc-lean-generic-stepper", RunnerCapacity))
            {
                var counters = new int[ConcurrentTasks];

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                            new CountingStructTask(i, counters, StepsPerTask).RunOn(runner);

                        while (runner.hasTasks)
                            runner.Step();
                    }
                });

                AssertZero(allocated, $"Lean generic SteppableRunner struct tasks {Waves} waves x {ConcurrentTasks}");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(counters[i], Is.GreaterThanOrEqualTo(Waves * StepsPerTask * MeasureTotalExecutions), $"counter {i}");
            }
        }

        [Test]
        public void Lean_SteppableRunner_IdleSteps_AreZeroAllocation()
        {
            using (var runner = new Lean.SteppableRunner("zeroalloc-lean-idle", RunnerCapacity))
            {
                long allocated = Measure(() =>
                {
                    for (int i = 0; i < IdleSteps; i++)
                        runner.Step();
                });

                AssertZero(allocated, $"{IdleSteps} idle Step() calls");
            }
        }

        [Test]
        public void Lean_SteppableRunner_PauseResumeCycles_AreZeroAllocation()
        {
            using (var runner = new Lean.SteppableRunner("zeroalloc-lean-pauseresume", RunnerCapacity))
            {
                var tasks = CreateLeanTasks();

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        runner.Pause();

                        SubmitAll(runner, tasks);

                        runner.Resume();

                        while (runner.hasTasks)
                            runner.Step();
                    }
                });

                AssertZero(allocated, "Lean SteppableRunner pause/resume cycles");
                AssertAllCompleted(tasks, Waves * MeasureTotalExecutions);
            }
        }

        [Test]
        public void Lean_SteppableRunner_ContinueChains_AreZeroAllocation()
        {
            using (var runner = new Lean.SteppableRunner("zeroalloc-lean-continue", RunnerCapacity))
            {
                var children = CreateLeanTasks(ChildTasks);
                var parent = new ParentContinuesChildren(children);

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        parent.Reset();
                        parent.RunOn(runner);

                        while (runner.hasTasks)
                            runner.Step();
                    }
                });

                AssertZero(allocated,
                    $"Lean SteppableRunner parent spawning {ChildTasks} .Continue() children x {Waves} waves");

                Assert.That(parent.completedRuns, Is.GreaterThanOrEqualTo(Waves * MeasureTotalExecutions));
                AssertAllCompleted(children, Waves * MeasureTotalExecutions);
            }
        }

        [Test]
        public void Lean_SteppableRunner_StaggeredFlow_IsZeroAllocation()
        {
            using (var runner = new Lean.SteppableRunner("zeroalloc-lean-staggered", RunnerCapacity))
            {
                runner.UseFlowModifier(new StaggeredFlow(4));

                var tasks = CreateLeanTasks();

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        SubmitAll(runner, tasks);

                        while (runner.hasTasks)
                            runner.Step();
                    }
                });

                AssertZero(allocated, "Lean SteppableRunner with StaggeredFlow");
                AssertAllCompleted(tasks, Waves * MeasureTotalExecutions);
            }
        }

        [Test]
        public void Lean_SteppableRunner_SerialFlow_IsZeroAllocation()
        {
            using (var runner = new Lean.SteppableRunner("zeroalloc-lean-serial", RunnerCapacity))
            {
                runner.UseFlowModifier(new SerialFlow());

                var tasks = CreateLeanTasks();

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        SubmitAll(runner, tasks);

                        while (runner.hasTasks)
                            runner.Step();
                    }
                });

                AssertZero(allocated, "Lean SteppableRunner with SerialFlow");
                AssertAllCompleted(tasks, Waves * MeasureTotalExecutions);
            }
        }

        [Test]
        public void Lean_SyncRunner_Instance_IsZeroAllocation()
        {
            using (var runner = new Lean.SyncRunner("zeroalloc-lean-sync"))
            {
                var task = new ReusableLeanTask();

                long allocated = Measure(() =>
                {
                    for (int i = 0; i < Waves; i++)
                    {
                        task.Restart(StepsPerTask);
                        task.RunOn(runner);
                        runner.WaitForTasksDoneRelaxed();
                    }
                });

                AssertZero(allocated, "explicit Lean SyncRunner RunOn + wait cycles");
                Assert.That(task.completedRuns, Is.GreaterThanOrEqualTo(Waves * MeasureTotalExecutions));
            }
        }

        [Test]
        public void Lean_GenericSyncRunner_StructTasks_AreZeroAllocation()
        {
            using (var runner = new Lean.SyncRunner<CountingStructTask>("zeroalloc-lean-generic-sync"))
            {
                var counters = new int[ConcurrentTasks];

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                            Lean.TaskRunnerExtensions.RunOn(new CountingStructTask(i, counters, StepsPerTask), runner);

                        runner.WaitForTasksDoneRelaxed();
                    }
                });

                AssertZero(allocated, $"Lean generic SyncRunner struct tasks {Waves} waves x {ConcurrentTasks}");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(counters[i], Is.GreaterThanOrEqualTo(Waves * StepsPerTask * MeasureTotalExecutions), $"counter {i}");
            }
        }

        [Test]
        public void Complete_Extension_ThreadLocalSyncRunner_IsZeroAllocation()
        {
            var task = new ReusableLeanTask();

            long allocated = Measure(() =>
            {
                for (int i = 0; i < Waves; i++)
                {
                    task.Restart(StepsPerTask);
                    ((IEnumerator<TaskContract>)task).Complete();
                }
            });

            AssertZero(allocated, $".Complete() convenience extension x {Waves}");
            Assert.That(task.completedRuns, Is.GreaterThanOrEqualTo(Waves * MeasureTotalExecutions));
        }

        [Test]
        public void Lean_MultiThreadRunner_SubmitAndWait_MainThread_IsZeroAllocation()
        {
            using (var runner = new Lean.MultiThreadRunner("zeroalloc-lean-mt", false, false, RunnerCapacity))
            {
                var tasks = CreateLeanTasks();
                bool allDone = true;

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                        {
                            tasks[i].Restart(StepsPerTask);
                            tasks[i].RunOn(runner);
                        }

                        allDone &= runner.WaitForTasksDone(MtTimeoutMs);
                    }
                });

                AssertZero(allocated,
                    $"Lean MultiThreadRunner submission from main thread x {MtWaves} waves x {ConcurrentTasks} tasks");
                Assert.That(allDone, Is.True, "tasks did not complete within the timeout");
                AssertAllCompleted(tasks, MtWaves * MeasureTotalExecutions);
            }
        }

        [Test]
        public void Lean_GenericMultiThreadRunner_StructTasks_MainThread_IsZeroAllocation()
        {
            using (var runner = new Lean.MultiThreadRunner<CountingStructTask>(
                       "zeroalloc-lean-generic-mt", false, false, RunnerCapacity))
            {
                var counters = new int[ConcurrentTasks];
                bool allDone = true;

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                            Lean.TaskRunnerExtensions.RunOn(new CountingStructTask(i, counters, StepsPerTask), runner);

                        allDone &= runner.WaitForTasksDone(MtTimeoutMs);
                    }
                });

                AssertZero(allocated,
                    $"Lean generic MultiThreadRunner struct submission x {MtWaves} waves x {ConcurrentTasks} tasks");
                Assert.That(allDone, Is.True, "tasks did not complete within the timeout");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(counters[i], Is.GreaterThanOrEqualTo(MtWaves * StepsPerTask * MeasureTotalExecutions), $"counter {i}");
            }
        }

        [Test]
        public void Lean_MultiThreadRunner_WorkerThread_IsZeroAllocation()
        {
            using (var runner = new Lean.MultiThreadRunner("zeroalloc-lean-mt-worker", false, false, RunnerCapacity))
            {
                var probeA = new AllocationProbe();
                var probeB = new AllocationProbe();
                var probeEnd = new AllocationProbe();
                long workerBytes = 0;

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        probeA.Restart();
                        probeB.Restart();
                        probeEnd.Restart();

                        probeA.RunOn(runner);
                        probeB.RunOn(runner);
                        probeEnd.RunOn(runner);

                        //the end marker's FIRST MoveNext runs on the worker only after probeB fully
                        //completed and its cleanup ran, so the window includes B's disposal
                        long end;
                        while ((end = probeEnd.startBytes) < 0)
                            Thread.Sleep(1);

                        workerBytes += end - probeA.startBytes;
                    }
                }, warmupRuns: 5);

                AssertZero(workerBytes,
                    "Lean MultiThreadRunner worker thread: scheduling + stepping + completion of the probe tasks " +
                    "(the end marker brackets everything probeB executes, including its completion cleanup)");
                AssertZero(allocated, "Lean MultiThreadRunner main thread while driving worker waves");
            }
        }

        [Test]
        public void ExtraLean_SteppableRunner_ClassTasks_AreZeroAllocation()
        {
            using (var runner = new ExtraLean.SteppableRunner("zeroalloc-extralean-stepper", RunnerCapacity))
            {
                var tasks = new ReusableExtraLeanTask[ConcurrentTasks];
                for (int i = 0; i < ConcurrentTasks; i++)
                    tasks[i] = new ReusableExtraLeanTask();

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                        {
                            tasks[i].Restart(StepsPerTask);
                            tasks[i].RunOn(runner);
                        }

                        while (runner.hasTasks)
                            runner.Step();
                    }
                });

                AssertZero(allocated, $"ExtraLean SteppableRunner {Waves} waves x {ConcurrentTasks} tasks");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(tasks[i].completedRuns, Is.GreaterThanOrEqualTo(Waves * MeasureTotalExecutions), $"task {i}");
            }
        }

        [Test]
        public void ExtraLean_GenericSteppableRunner_StructTasks_AreZeroAllocation()
        {
            using (var runner = new ExtraLean.SteppableRunner<CountingExtraLeanStructTask>(
                       "zeroalloc-extralean-generic-stepper", RunnerCapacity))
            {
                var counters = new int[ConcurrentTasks];

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                            new CountingExtraLeanStructTask(i, counters, StepsPerTask).RunOn(runner);

                        while (runner.hasTasks)
                            runner.Step();
                    }
                });

                AssertZero(allocated, $"ExtraLean generic SteppableRunner struct tasks {Waves} waves x {ConcurrentTasks}");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(counters[i], Is.GreaterThanOrEqualTo(Waves * StepsPerTask * MeasureTotalExecutions), $"counter {i}");
            }
        }

        [Test]
        public void ExtraLean_SyncRunner_ClassTasks_AreZeroAllocation()
        {
            using (var runner = new ExtraLean.SyncRunner("zeroalloc-extralean-sync"))
            {
                var tasks = new ReusableExtraLeanTask[ConcurrentTasks];
                for (int i = 0; i < ConcurrentTasks; i++)
                    tasks[i] = new ReusableExtraLeanTask();

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                        {
                            tasks[i].Restart(StepsPerTask);
                            ExtraLean.TaskRunnerExtensions.RunOn((IEnumerator)tasks[i], runner);
                        }

                        runner.WaitForTasksDoneRelaxed();
                    }
                });

                AssertZero(allocated, $"ExtraLean SyncRunner class tasks {Waves} waves x {ConcurrentTasks}");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(tasks[i].completedRuns, Is.GreaterThanOrEqualTo(Waves * MeasureTotalExecutions), $"task {i}");
            }
        }

        [Test]
        public void ExtraLean_GenericSyncRunner_StructTasks_AreZeroAllocation()
        {
            using (var runner = new ExtraLean.SyncRunner<CountingExtraLeanStructTask>(
                       "zeroalloc-extralean-generic-sync"))
            {
                var counters = new int[ConcurrentTasks];

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < Waves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                            ExtraLean.TaskRunnerExtensions.RunOn(
                                new CountingExtraLeanStructTask(i, counters, StepsPerTask), runner);

                        runner.WaitForTasksDoneRelaxed();
                    }
                });

                AssertZero(allocated,
                    $"ExtraLean generic SyncRunner struct tasks {Waves} waves x {ConcurrentTasks}");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(counters[i], Is.GreaterThanOrEqualTo(Waves * StepsPerTask * MeasureTotalExecutions), $"counter {i}");
            }
        }

        [Test]
        public void ExtraLean_MultiThreadRunner_SubmitAndWait_MainThread_IsZeroAllocation()
        {
            using (var runner = new ExtraLean.MultiThreadRunner("zeroalloc-extralean-mt", false, false, RunnerCapacity))
            {
                var tasks = new ReusableExtraLeanTask[ConcurrentTasks];
                for (int i = 0; i < ConcurrentTasks; i++)
                    tasks[i] = new ReusableExtraLeanTask();

                bool allDone = true;

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                        {
                            tasks[i].Restart(StepsPerTask);
                            tasks[i].RunOn(runner);
                        }

                        allDone &= runner.WaitForTasksDone(MtTimeoutMs);
                    }
                });

                AssertZero(allocated,
                    $"ExtraLean MultiThreadRunner submission from main thread x {MtWaves} waves x {ConcurrentTasks} tasks");
                Assert.That(allDone, Is.True, "tasks did not complete within the timeout");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(tasks[i].completedRuns, Is.GreaterThanOrEqualTo(MtWaves * MeasureTotalExecutions), $"task {i}");
            }
        }

        [Test]
        public void ExtraLean_GenericClassMultiThreadRunner_MainThread_IsZeroAllocation()
        {
            using (var runner = new ExtraLean.MultiThreadRunner<ReusableExtraLeanTask>(
                       "zeroalloc-extralean-class-mt", false, false, RunnerCapacity))
            {
                var tasks = new ReusableExtraLeanTask[ConcurrentTasks];
                for (int i = 0; i < ConcurrentTasks; i++)
                    tasks[i] = new ReusableExtraLeanTask();

                bool allDone = true;

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                        {
                            tasks[i].Restart(StepsPerTask);
                            ExtraLean.TaskRunnerExtensionsRef.RunOn(tasks[i], runner);
                        }

                        allDone &= runner.WaitForTasksDone(MtTimeoutMs);
                    }
                });

                AssertZero(allocated,
                    $"ExtraLean typed class MultiThreadRunner x {MtWaves} waves x {ConcurrentTasks} tasks");
                Assert.That(allDone, Is.True, "tasks did not complete within the timeout");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(tasks[i].completedRuns, Is.GreaterThanOrEqualTo(MtWaves * MeasureTotalExecutions), $"task {i}");
            }
        }

        [Test]
        public void ExtraLean_GenericStructMultiThreadRunner_MainThread_IsZeroAllocation()
        {
            using (var runner = new ExtraLean.Struct.MultiThreadRunner<CountingExtraLeanStructTask>(
                       "zeroalloc-extralean-struct-mt", false, false, RunnerCapacity))
            {
                var counters = new int[ConcurrentTasks];
                bool allDone = true;

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                            ExtraLean.TaskRunnerExtensions.RunOn(
                                new CountingExtraLeanStructTask(i, counters, StepsPerTask), runner);

                        allDone &= runner.WaitForTasksDone(MtTimeoutMs);
                    }
                });

                AssertZero(allocated,
                    $"ExtraLean typed struct MultiThreadRunner x {MtWaves} waves x {ConcurrentTasks} tasks");
                Assert.That(allDone, Is.True, "tasks did not complete within the timeout");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(counters[i], Is.GreaterThanOrEqualTo(MtWaves * StepsPerTask * MeasureTotalExecutions), $"counter {i}");
            }
        }

        [Test]
        public void ExtraLean_MultiThreadRunner_WorkerThread_IsZeroAllocation()
        {
            using (var runner = new ExtraLean.MultiThreadRunner("zeroalloc-extralean-mt-worker", false, false, RunnerCapacity))
            {
                var probeA = new ExtraLeanAllocationProbe();
                var probeB = new ExtraLeanAllocationProbe();
                var probeEnd = new ExtraLeanAllocationProbe();
                long workerBytes = 0;

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        probeA.Restart();
                        probeB.Restart();
                        probeEnd.Restart();

                        probeA.RunOn(runner);
                        probeB.RunOn(runner);
                        probeEnd.RunOn(runner);

                        //the end marker's FIRST MoveNext runs on the worker only after probeB fully
                        //completed and its cleanup ran, so the window includes B's disposal
                        long end;
                        while ((end = probeEnd.startBytes) < 0)
                            Thread.Sleep(1);

                        workerBytes += end - probeA.startBytes;
                    }
                }, warmupRuns: 5);

                AssertZero(workerBytes,
                    "ExtraLean MultiThreadRunner worker thread: scheduling + stepping + completion of two probe tasks");
                AssertZero(allocated, "ExtraLean MultiThreadRunner main thread while driving worker waves");
            }
        }

        [Test]
        public void ExtraLean_MultiThreadRunnerPool_Dispatch_IsZeroAllocation()
        {
            using (var pool = new ExtraLean.MultiThreadRunnerPool("zeroalloc-pool", 4, RunnerCapacity))
            {
                var tasks = new ReusableExtraLeanTask[ConcurrentTasks];
                for (int i = 0; i < ConcurrentTasks; i++)
                    tasks[i] = new ReusableExtraLeanTask();

                bool timedOut = false;

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                        {
                            tasks[i].Restart(StepsPerTask);
                            tasks[i].RunOn(pool);
                        }

                        var spinner = new SpinWait();
                        var deadline = DateTime.UtcNow.AddSeconds(10);
                        while (PoolTasksDone(tasks, wave + 1) == false)
                        {
                            if (DateTime.UtcNow > deadline)
                            {
                                timedOut = true;
                                break;
                            }

                            if (spinner.NextSpinWillYield)
                                Thread.Sleep(1);
                            spinner.SpinOnce();
                        }
                    }
                });

                AssertZero(allocated,
                    $"MultiThreadRunnerPool dispatch of {MtWaves} waves x {ConcurrentTasks} root tasks");
                Assert.That(timedOut, Is.False, "pool tasks did not complete");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(tasks[i].completedRuns, Is.GreaterThanOrEqualTo(MtWaves * MeasureTotalExecutions), $"task {i}");
            }
        }

        [Test]
        public void ExtraLean_GenericMultiThreadRunnerPool_StructDispatch_IsZeroAllocation()
        {
            using (var pool = new ExtraLean.MultiThreadRunnerPool<CountingExtraLeanStructTask>(
                       "zeroalloc-typed-pool", 4, false))
            {
                var counters = new int[ConcurrentTasks];
                bool timedOut = false;

                long allocated = Measure(() =>
                {
                    Array.Clear(counters, 0, counters.Length);

                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        for (int i = 0; i < ConcurrentTasks; i++)
                            ExtraLean.TaskRunnerExtensions.RunOn(
                                new CountingExtraLeanStructTask(i, counters, StepsPerTask), pool);

                        var deadline = DateTime.UtcNow.AddSeconds(10);
                        int expected = (wave + 1) * StepsPerTask;
                        while (StructPoolTasksDone(counters, expected) == false)
                        {
                            if (DateTime.UtcNow > deadline)
                            {
                                timedOut = true;
                                break;
                            }

                            Thread.Sleep(1);
                        }
                    }
                });

                AssertZero(allocated,
                    $"typed MultiThreadRunnerPool dispatch of {MtWaves} waves x {ConcurrentTasks} struct tasks");
                Assert.That(timedOut, Is.False, "typed pool tasks did not complete");

                //the workload CLEARS the counters on every execution, so after Measure they hold exactly
                //the measured run's totals: do not scale by MeasureTotalExecutions here
                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(counters[i], Is.GreaterThanOrEqualTo(MtWaves * StepsPerTask), $"counter {i}");
            }
        }

        [Test]
        public void MultiThreadedParallelJobCollection_Waves_MainThread_IsZeroAllocation()
        {
            using (var collection =
                       new MultiThreadedParallelJobCollection<CountingJob>("zeroalloc-jobs", CollectionThreads, false))
            {
                var results = new long[JobIterations];

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        collection.Reset();
                        collection.Add(new CountingJob { results = results }, JobIterations);

                        while (collection.MoveNext())
                        {
                        }
                    }
                });

                AssertZero(allocated,
                    $"MultiThreadedParallelJobCollection {MtWaves} waves of {JobIterations} iterations " +
                    $"across {CollectionThreads} threads (submission and spin on the main thread)");

                for (int i = 0; i < JobIterations; i++)
                    Assert.That(results[i], Is.GreaterThanOrEqualTo(MtWaves * MeasureTotalExecutions), $"iteration {i}");
            }
        }

        [Test]
        public void ExtraLean_MultiThreadedParallelTaskCollection_Waves_MainThread_IsZeroAllocation()
        {
            using (var collection =
                       new Svelto.Tasks.Parallelism.ExtraLean.MultiThreadedParallelTaskCollection(
                           "zeroalloc-parallel-tasks", CollectionThreads, false))
            {
                var tasks = new ReusableParallelTask[ConcurrentTasks];
                for (int i = 0; i < ConcurrentTasks; i++)
                    tasks[i] = new ReusableParallelTask();

                long allocated = Measure(() =>
                {
                    for (int wave = 0; wave < MtWaves; wave++)
                    {
                        collection.Reset();

                        for (int i = 0; i < ConcurrentTasks; i++)
                        {
                            tasks[i].Restart(StepsPerTask);
                            collection.Add(tasks[i]);
                        }

                        while (collection.MoveNext())
                        {
                        }
                    }
                });

                AssertZero(allocated,
                    $"ExtraLean MultiThreadedParallelTaskCollection {MtWaves} waves x {ConcurrentTasks} tasks " +
                    $"across {CollectionThreads} threads (submission and spin on the main thread)");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(tasks[i].completedRuns, Is.GreaterThanOrEqualTo(MtWaves * MeasureTotalExecutions), $"task {i}");
            }
        }

        static ReusableLeanTask[] CreateLeanTasks(int count = ConcurrentTasks)
        {
            var tasks = new ReusableLeanTask[count];

            for (int i = 0; i < count; i++)
                tasks[i] = new ReusableLeanTask();

            return tasks;
        }

        static void SubmitAll(Lean.SteppableRunner runner, ReusableLeanTask[] tasks)
        {
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i].Restart(StepsPerTask);
                tasks[i].RunOn(runner);
            }
        }

        static void AssertAllCompleted(ReusableLeanTask[] tasks, int expectedRuns)
        {
            for (int i = 0; i < tasks.Length; i++)
                Assert.That(tasks[i].completedRuns, Is.GreaterThanOrEqualTo(expectedRuns), $"task {i}");
        }

        static bool PoolTasksDone(ReusableExtraLeanTask[] tasks, int expected)
        {
            for (int i = 0; i < tasks.Length; i++)
            {
                if (Volatile.Read(ref tasks[i].completionCounter) < expected)
                    return false;
            }

            return true;
        }

        static bool StructPoolTasksDone(int[] counters, int expected)
        {
            for (int i = 0; i < counters.Length; i++)
            {
                if (Volatile.Read(ref counters[i]) < expected)
                    return false;
            }

            return true;
        }

        sealed class ReusableLeanTask : IEnumerator<TaskContract>
        {
            internal ReusableLeanTask() { }

            internal int completedRuns { get; private set; }

            internal void Restart(int steps)
            {
                _totalSteps = steps;
                _stepsLeft  = steps;
            }

            public bool MoveNext()
            {
                if (_stepsLeft == 0)
                {
                    completedRuns++;

                    return false;
                }

                --_stepsLeft;
                Current = TaskContract.Yield.It;

                return true;
            }

            public TaskContract Current { get; private set; }
            object IEnumerator.Current => throw new NotSupportedException();
            public void Reset() { _stepsLeft = _totalSteps; }
            public void Dispose() { }

            int _stepsLeft;
            int _totalSteps;
        }

        struct CountingStructTask : IEnumerator<TaskContract>, IEquatable<CountingStructTask>
        {
            public CountingStructTask(int id, int[] counters, int steps) : this()
            {
                _id        = id;
                _counters  = counters;
                _stepsLeft = steps;
                _valid     = true;
                Current    = TaskContract.Yield.It;
            }

            public bool MoveNext()
            {
                if (_stepsLeft == 0)
                    return false;

                --_stepsLeft;
                ++_counters[_id];
                Current = TaskContract.Yield.It;

                return true;
            }

            public TaskContract Current { get; private set; }
            object IEnumerator.Current => Current;
            public void Reset() { }
            public void Dispose() { }

            public bool Equals(CountingStructTask other) => _valid == other._valid && _id == other._id &&
                                                             _stepsLeft == other._stepsLeft;
            public override bool Equals(object obj) => obj is CountingStructTask other && Equals(other);
            public override int GetHashCode() => _id;

            readonly int[] _counters;
            readonly int   _id;
            readonly bool  _valid;
            int            _stepsLeft;
        }

        sealed class ParentContinuesChildren : IEnumerator<TaskContract>
        {
            internal ParentContinuesChildren(ReusableLeanTask[] children)
            {
                _children = children;
            }

            internal int completedRuns { get; private set; }

            public void Reset()
            {
                _next = 0;

                for (int i = 0; i < _children.Length; i++)
                    _children[i].Restart(StepsPerTask);
            }

            public bool MoveNext()
            {
                if (_next < _children.Length)
                {
                    Current = _children[_next++].Continue();

                    return true;
                }

                ++completedRuns;

                return false;
            }

            public TaskContract Current { get; private set; }
            object IEnumerator.Current => throw new NotSupportedException();
            public void Dispose() { }

            readonly ReusableLeanTask[] _children;
            int _next;
        }

        sealed class AllocationProbe : IEnumerator<TaskContract>
        {
            internal const int ProbeSteps = 4;

            internal long startBytes => Volatile.Read(ref _start);
            internal long endBytes   => Volatile.Read(ref _end);

            internal void Restart()
            {
                _stepsLeft = ProbeSteps;
                _start     = -1;
                _end       = -1;
            }

            public bool MoveNext()
            {
                if (_stepsLeft == ProbeSteps)
                    Volatile.Write(ref _start, GC.GetAllocatedBytesForCurrentThread());

                --_stepsLeft;

                if (_stepsLeft == 0)
                    Volatile.Write(ref _end, GC.GetAllocatedBytesForCurrentThread());

                Current = TaskContract.Yield.It;

                return _stepsLeft > 0;
            }

            public TaskContract Current { get; private set; }
            object IEnumerator.Current => throw new NotSupportedException();
            public void Reset() { }
            public void Dispose() { }

            long _start;
            long _end;
            int  _stepsLeft;
        }

        sealed class ReusableExtraLeanTask : IEnumerator
        {
            internal int completedRuns => Volatile.Read(ref completionCounter);

            internal void Restart(int steps)
            {
                _totalSteps = steps;
                _stepsLeft  = steps;
            }

            public bool MoveNext()
            {
                if (_stepsLeft == 0)
                {
                    Interlocked.Increment(ref completionCounter);

                    return false;
                }

                --_stepsLeft;

                return true;
            }

            public object Current => null;
            public void Reset() { _stepsLeft = _totalSteps; }
            public void Dispose() { }

            internal int completionCounter;

            int _stepsLeft;
            int _totalSteps;
        }

        struct CountingExtraLeanStructTask : IEnumerator, IDisposable
        {
            public CountingExtraLeanStructTask(int id, int[] counters, int steps) : this()
            {
                _id        = id;
                _counters  = counters;
                _stepsLeft = steps;
            }

            public bool MoveNext()
            {
                if (_stepsLeft == 0)
                    return false;

                --_stepsLeft;
                ++_counters[_id];

                return true;
            }

            public object Current => null;
            public void Reset() { }
            public void Dispose() { }

            readonly int[] _counters;
            readonly int   _id;
            int            _stepsLeft;
        }

        sealed class ExtraLeanAllocationProbe : IEnumerator
        {
            internal long startBytes => Volatile.Read(ref _start);
            internal long endBytes   => Volatile.Read(ref _end);

            internal void Restart()
            {
                _stepsLeft = AllocationProbe.ProbeSteps;
                _start     = -1;
                _end       = -1;
            }

            public bool MoveNext()
            {
                if (_stepsLeft == AllocationProbe.ProbeSteps)
                    Volatile.Write(ref _start, GC.GetAllocatedBytesForCurrentThread());

                --_stepsLeft;

                if (_stepsLeft == 0)
                    Volatile.Write(ref _end, GC.GetAllocatedBytesForCurrentThread());

                return _stepsLeft > 0;
            }

            public object Current => null;
            public void Reset() { }
            public void Dispose() { }

            long _start;
            long _end;
            int  _stepsLeft;
        }

        struct CountingJob : ISveltoJob
        {
            public long[] results;

            public void Update(int index)
            {
                ++results[index];
            }

            public void Dispose() { }
        }

        sealed class ReusableParallelTask : IEnumerator, IParallelTask
        {
            internal int completedRuns { get; private set; }

            internal void Restart(int steps)
            {
                _totalSteps = steps;
                _stepsLeft  = steps;
            }

            public bool MoveNext()
            {
                if (_stepsLeft == 0)
                {
                    ++completedRuns;

                    return false;
                }

                --_stepsLeft;

                return true;
            }

            public object Current => null;
            public void Reset() { _stepsLeft = _totalSteps; }

            public void Dispose()
            {
                _stepsLeft = _totalSteps;
            }

            int _stepsLeft;
            int _totalSteps;
        }
    }
}



