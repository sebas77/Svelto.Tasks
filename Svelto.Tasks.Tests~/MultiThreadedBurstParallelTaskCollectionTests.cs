using System;
using System.Runtime.InteropServices;
using System.Threading;
using Svelto.Tasks.Parallelism;
using Svelto.Tasks.Parallelism.ExtraLean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public unsafe class MultiThreadedBurstParallelTaskCollectionTests
    {
#if DEBUG && !DISABLE_DBC && !PROFILE_SVELTO
        [Test]
        public void MultiThreadedBurstParallelTaskCollection_ZeroThreads_Throws()
        {
            Assert.Throws<DBC.Tasks.PreconditionException>(() =>
                new MultiThreadedBurstParallelTaskCollection<BurstRangeJob>("burst_zero_threads", 0, false));
        }
#endif

        [Test]
        public void MultiThreadedBurstParallelTaskCollection_SplitsIterations_IncludingPartialLastChunk()
        {
            // What we are testing:
            // 1000 iterations with 300 elements per chunk produce 4 chunks, the last one
            // holding the remaining 100 elements. Every index must be processed exactly once.

            using (var results = new SharedResults(1000))
            {
                int completions = 0;

                using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstRangeJob>("burst_chunks", 4, false))
                {
                    collection.onComplete += () => completions++;

                    collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter), 1000, 300);

                    collection.Run().Complete(5000);

                    AssertEveryIndexEqualTo(results, 1000, 1);
                    Assert.That(completions, Is.EqualTo(1));
                }

                // 1 stored prototype disposed by Dispose + 4 chunk copies disposed at completion
                Assert.That(results.Disposes, Is.EqualTo(5));
            }
        }

        [Test]
        public void MultiThreadedBurstParallelTaskCollection_RunsChunksInParallel()
        {
            // What we are testing:
            // One chunk per element is dispatched to different threads, so 4 sleeping
            // chunks take about one chunk duration instead of 4 times that.

            using (var results = new SharedResults(4))
            using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstRangeJob>("burst_parallel", 4, false))
            {
                collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter, sleepMs: 300), 4, 1);

                DateTime now = DateTime.Now;

                collection.Run().Complete(5000);

                var totalSeconds = (DateTime.Now - now).TotalSeconds;

                Assert.That(totalSeconds, Is.GreaterThan(0.25));
                Assert.That(totalSeconds, Is.LessThan(1.1)); // sequential execution would take 1.2s
                AssertEveryIndexEqualTo(results, 4, 1);
            }
        }

        [Test]
        public void MultiThreadedBurstParallelTaskCollection_MultipleAdds_CoverAllDefinitions()
        {
            // What we are testing:
            // Every Add() defines an independent range task starting at 0. Two Adds over
            // 200 and 250 iterations execute indices 0..199 twice, 200..249 once.

            using (var results = new SharedResults(450))
            {
                using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstRangeJob>("burst_multi_add", 4, false))
                {
                    collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter), 200, 100);
                    collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter), 250, 100);

                    collection.Run().Complete(5000);

                    for (int i = 0; i < 450; i++)
                    {
                        int expected = i < 200 ? 2 : i < 250 ? 1 : 0;
                        Assert.That(results[i], Is.EqualTo(expected), $"index {i}");
                    }
                }

                Assert.That(results.Disposes, Is.EqualTo(7)); // 2 prototypes + 5 chunk copies
            }
        }

#if DEBUG && !DISABLE_DBC && !PROFILE_SVELTO
        [Test]
        public void MultiThreadedBurstParallelTaskCollection_Add_WithNonPositiveElementsPerTask_Throws()
        {
            using (var results = new SharedResults(1))
            using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstRangeJob>("burst_guard", 2, false))
            {
                Assert.Throws<DBC.Tasks.PreconditionException>(() =>
                    collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter), 100, 0));
                Assert.Throws<DBC.Tasks.PreconditionException>(() =>
                    collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter), 100, -1));
            }
        }
#endif

        [Test]
        public void MultiThreadedBurstParallelTaskCollection_Add_WithNonPositiveIterations_IsIgnored()
        {
            using (var results = new SharedResults(1))
            using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstRangeJob>("burst_noop", 2, false))
            {
                int completions = 0;
                collection.onComplete += () => completions++;

                collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter), 0, 10);
                collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter), -1, 10);

                collection.Run().Complete(1000);

                Assert.That(completions, Is.EqualTo(1));
                Assert.That(results[0], Is.EqualTo(0));
                Assert.That(results.Disposes, Is.EqualTo(0)); // no definitions were stored
            }
        }

#if DEBUG && !DISABLE_DBC && !PROFILE_SVELTO
        [Test]
        public void MultiThreadedBurstParallelTaskCollection_Constructor_WithZeroThreads_Throws()
        {
            Assert.Throws<DBC.Tasks.PreconditionException>(() =>
                new MultiThreadedBurstParallelTaskCollection<BurstRangeJob>("burst_zero_threads_ctor", 0, false));
        }

        [Test]
        public void MultiThreadedBurstParallelTaskCollection_AddWhileRunning_Throws()
        {
            using (var results = new SharedResults(2))
            using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstSteppedJob>("burst_add_running", 2, false))
            {
                collection.Add(new BurstSteppedJob(results.Pointer, results.DisposeCounter, steps: 2, sleepMs: 50), 2, 1);

                collection.MoveNext();
                Assert.That(collection.isRunning, Is.True);

                Assert.Throws<DBC.Tasks.PreconditionException>(() =>
                    collection.Add(new BurstSteppedJob(results.Pointer, results.DisposeCounter, steps: 1, sleepMs: 0), 1, 1));

                collection.Run().Complete(2000);
            }
        }
#endif

        [Test]
        public void MultiThreadedBurstParallelTaskCollection_Stop_CancelsPendingChunks_AndRun_CanBeRestarted()
        {
            // What we are testing:
            // Stop cancels unclaimed chunks, in-flight ones finish cooperatively. A new run
            // resets cancellation and the cursor, so every chunk is processed exactly once
            // across the stop + restart pair (k processed before stop, 8 after restart).

            using (var results = new SharedResults(8))
            using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstRangeJob>("burst_stop", 2, false))
            {
                collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter, sleepMs: 150), 8, 1);

                collection.MoveNext();
                Assert.That(collection.isRunning, Is.True);

                Thread.Sleep(50);
                collection.Stop(2000);
                Assert.That(collection.isRunning, Is.False);

                int processedBeforeStop = Sum(results, 8);
                Assert.That(processedBeforeStop, Is.LessThan(8));
                AssertEveryIndexAtMost(results, 8, 1);

                while (collection.MoveNext()) { }

                AssertEveryIndexAtLeast(results, 8, 1);
                Assert.That(Sum(results, 8), Is.EqualTo(8 + processedBeforeStop));
            }
        }

        [Test]
        public void MultiThreadedBurstParallelTaskCollection_MultiStepChunks_RunAllSteps()
        {
            // What we are testing:
            // A range task whose MoveNext returns true keeps its claimed range across
            // dispatcher steps; the whole range executes once per step.

            using (var results = new SharedResults(2))
            {
                using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstSteppedJob>("burst_multistep", 1, false))
                {
                    collection.Add(new BurstSteppedJob(results.Pointer, results.DisposeCounter, steps: 3, sleepMs: 0), 2, 1);

                    collection.Run().Complete(5000);

                    AssertEveryIndexEqualTo(results, 2, 3);
                }

                Assert.That(results.Disposes, Is.EqualTo(3)); // 1 stored prototype + 2 chunk copies
            }
        }

        [Test]
        public void MultiThreadedBurstParallelTaskCollection_Dispose_DuringRun_StopsCollection()
        {
            // What we are testing:
            // Disposing mid-run unwinds in-flight chunks cooperatively and leaves the
            // collection unusable but safe.

            using (var results = new SharedResults(6))
            using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstSteppedJob>("burst_dispose_run", 2, false))
            {
                collection.Add(new BurstSteppedJob(results.Pointer, results.DisposeCounter, steps: 3, sleepMs: 50), 6, 1);

                collection.MoveNext();
                Assert.That(collection.isRunning, Is.True);

                collection.Dispose();

                Assert.That(collection.MoveNext(), Is.False);
                Assert.That(results.Disposes, Is.GreaterThanOrEqualTo(1)); // the stored prototype
            }
        }

        [Test]
        public void MultiThreadedBurstParallelTaskCollection_Dispose_WithoutRunning_DisposesEachPrototype()
        {
            using (var results = new SharedResults(2))
            using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstRangeJob>("burst_dispose_idle", 2, false))
            {
                collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter), 1, 1);
                collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter), 1, 1);

                collection.Dispose();

                Assert.That(results.Disposes, Is.EqualTo(2)); // one prototype per Add
                Assert.That(collection.MoveNext(), Is.False);
            }
        }

        [Test]
        public void MultiThreadedBurstParallelTaskCollection_Run_SupportsMultipleWaves()
        {
            // What we are testing:
            // The same definitions can be rerun: BeginRun resets the shared cursor, so a
            // second wave processes every chunk again.

            using (var results = new SharedResults(4))
            {
                using (var collection = new MultiThreadedBurstParallelTaskCollection<BurstRangeJob>("burst_waves", 2, false))
                {
                    int completions = 0;
                    collection.onComplete += () => completions++;

                    collection.Add(new BurstRangeJob(results.Pointer, results.DisposeCounter), 4, 1);

                    while (collection.MoveNext()) { }
                    while (collection.MoveNext()) { }

                    AssertEveryIndexEqualTo(results, 4, 2);
                    Assert.That(completions, Is.EqualTo(2));
                }

                Assert.That(results.Disposes, Is.EqualTo(9)); // 1 prototype + 4 chunk copies per wave
            }
        }

        static void AssertEveryIndexEqualTo(SharedResults results, int iterations, int expected)
        {
            for (int i = 0; i < iterations; i++)
                Assert.That(results[i], Is.EqualTo(expected), $"index {i}");
        }

        static void AssertEveryIndexAtLeast(SharedResults results, int iterations, int min)
        {
            for (int i = 0; i < iterations; i++)
                Assert.That(results[i], Is.GreaterThanOrEqualTo(min), $"index {i}");
        }

        static void AssertEveryIndexAtMost(SharedResults results, int iterations, int max)
        {
            for (int i = 0; i < iterations; i++)
                Assert.That(results[i], Is.LessThanOrEqualTo(max), $"index {i}");
        }

        static int Sum(SharedResults results, int iterations)
        {
            int sum = 0;
            for (int i = 0; i < iterations; i++)
                sum += results[i];
            return sum;
        }

        /// <summary>
        /// Unmanaged scratch memory shared with the unmanaged job structs: one int per
        /// iteration to count executions, plus one extra int used as dispose counter.
        /// </summary>
        unsafe sealed class SharedResults : IDisposable
        {
            public SharedResults(int size)
            {
                Size = size;
                _ptr = (int*)Marshal.AllocHGlobal((size + 1) * sizeof(int));
                Clear();
            }

            public int Size { get; }

            public int* Pointer => _ptr;

            public int* DisposeCounter => _ptr + Size;

            public int Disposes => Volatile.Read(ref _ptr[Size]);

            public int this[int index] => Volatile.Read(ref _ptr[index]);

            public void Clear()
            {
                for (int i = 0; i <= Size; i++)
                    _ptr[i] = 0;
            }

            public void Dispose()
            {
                if (_ptr != null)
                {
                    Marshal.FreeHGlobal((IntPtr)_ptr);
                    _ptr = null;
                }
            }

            int* _ptr;
        }

        /// <summary>
        /// Single-step unmanaged range task: increments results[startIndex..startIndex+count)
        /// once. sleepMs simulates an expensive Burst call for timing and cancellation tests.
        /// </summary>
        unsafe struct BurstRangeJob : IBurstParallelTask
        {
            public BurstRangeJob(int* results, int* disposeCounter, int sleepMs = 0)
            {
                _results        = results;
                _disposeCounter = disposeCounter;
                _sleepMs        = sleepMs;
                _startIndex     = 0;
                _count          = 0;
            }

            public void SetRange(int startIndex, int count)
            {
                _startIndex = startIndex;
                _count      = count;
            }

            public bool MoveNext()
            {
                if (_sleepMs > 0)
                    Thread.Sleep(_sleepMs);

                for (int i = _startIndex; i < _startIndex + _count; i++)
                    Interlocked.Increment(ref _results[i]);

                return false;
            }

            public void Dispose()
            {
                Interlocked.Increment(ref *_disposeCounter);
            }

            public void Reset() {}

            public object Current => null;

            readonly int* _results;
            readonly int* _disposeCounter;
            readonly int  _sleepMs;
            int           _startIndex;
            int           _count;
        }

        /// <summary>
        /// Multi-step unmanaged range task: increments its claimed range once per MoveNext
        /// and completes after Steps steps, mimicking range tasks spanning multiple runner
        /// iterations.
        /// </summary>
        unsafe struct BurstSteppedJob : IBurstParallelTask
        {
            public BurstSteppedJob(int* results, int* disposeCounter, int steps, int sleepMs)
            {
                _results        = results;
                _disposeCounter = disposeCounter;
                _steps          = steps;
                _sleepMs        = sleepMs;
                _stepsDone      = 0;
                _startIndex     = 0;
                _count          = 0;
            }

            public void SetRange(int startIndex, int count)
            {
                _startIndex = startIndex;
                _count      = count;
            }

            public bool MoveNext()
            {
                if (_sleepMs > 0)
                    Thread.Sleep(_sleepMs);

                for (int i = _startIndex; i < _startIndex + _count; i++)
                    Interlocked.Increment(ref _results[i]);

                return ++_stepsDone < _steps;
            }

            public void Dispose()
            {
                Interlocked.Increment(ref *_disposeCounter);
            }

            public void Reset() {}

            public object Current => null;

            readonly int* _results;
            readonly int* _disposeCounter;
            readonly int  _steps;
            readonly int  _sleepMs;
            int           _stepsDone;
            int           _startIndex;
            int           _count;
        }
    }
}
