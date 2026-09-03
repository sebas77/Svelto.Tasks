using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    /// <summary>
    /// Proves that a SteppableRunner preallocated for the expected number of concurrent struct
    /// tasks runs submit/step/complete waves with zero heap allocation after warm-up.
    /// Requires a Release build of Svelto.Tasks (dotnet test -c Release): the fixture detects a
    /// DEBUG/profiling library build and skips itself.
    /// </summary>
    [TestFixture]
    public class PreallocatedRunnerAllocationTests
    {
        const int ConcurrentTasks = 16;
        const int StepsPerTask = 8;
        const int Waves = 50;
        const uint RunnerCapacity = 32;

        [OneTimeSetUp]
        public void SkipUnlessLibraryIsReleaseBuild()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

            if (typeof(Enumerators.Continuation).GetField("_runner", flags) != null)
                Assert.Ignore(
                    "Svelto.Tasks is compiled with DEBUG: diagnostic allocations (WeakReference in " +
                    "Continuation, DBC strings) are enabled by design. Zero-allocation guarantees can " +
                    "only be verified against a Release build: dotnet test -c Release");
        }

        [Test]
        public void GenericSteppableRunner_StructTasks_AreZeroAllocation_AfterWarmUp()
        {
            var counters = new int[ConcurrentTasks];

            using (var runner = new SteppableRunner<WorkTask>("prealloc-zeroalloc-generic-stepper", RunnerCapacity))
            {
                RunWaves(runner, counters, 2);

                GC.Collect();
                GC.WaitForPendingFinalizers();

                long before = GC.GetAllocatedBytesForCurrentThread();

                RunWaves(runner, counters, Waves);

                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(allocated, Is.EqualTo(0),
                    "Preallocated generic struct-task runner allocated after warm-up. Ensure Svelto.Tasks " +
                    "is built in Release (dotnet test -c Release) and the runner capacity covers the " +
                    "number of concurrent tasks.");

                for (int i = 0; i < ConcurrentTasks; i++)
                    Assert.That(counters[i], Is.GreaterThanOrEqualTo((Waves + 2) * StepsPerTask), $"counter {i}");
            }
        }

        static void RunWaves(SteppableRunner<WorkTask> runner, int[] counters, int waveCount)
        {
            for (int wave = 0; wave < waveCount; wave++)
            {
                for (int i = 0; i < ConcurrentTasks; i++)
                    new WorkTask(i, counters).RunOn(runner);

                while (runner.hasTasks)
                    runner.Step();
            }
        }

        struct WorkTask : IEnumerator<TaskContract>, IEquatable<WorkTask>
        {
            public WorkTask(int id, int[] counters) : this()
            {
                _id = id;
                _counters = counters;
                _stepsLeft = StepsPerTask;
                _valid = true;
                Current = TaskContract.Yield.It;
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

            public bool Equals(WorkTask other) => _valid == other._valid && _id == other._id &&
                                                  _stepsLeft == other._stepsLeft;
            public override bool Equals(object obj) => obj is WorkTask other && Equals(other);
            public override int GetHashCode() => _id;

            readonly int[] _counters;
            readonly int   _id;
            readonly bool  _valid;
            int            _stepsLeft;
        }
    }
}
