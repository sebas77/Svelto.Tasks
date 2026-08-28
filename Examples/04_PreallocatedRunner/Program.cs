using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

#pragma warning disable CS0436

namespace Example04_PreallocatedRunner
{
    static class Program
    {
        const int TasksPerWave = 100;
        const int StepsPerTask = 3;

        static readonly int[] _workDone = new int[TasksPerWave];

        static void Main()
        {
            SafeTitle("Svelto.Tasks - Preallocated Runner");

            PrintBanner();
            WarmUpRuntime();

            //a default runner starts with capacity for 3 tasks: running a wave of 100 tasks
            //forces the internal containers to grow several times while the wave executes
            long defaultAllocations = MeasureWave(new SteppableRunner<WorkTask>("DefaultRunner"));

            //the capacity is sized to the expected number of concurrent tasks, so the
            //containers are big enough from the start and never grow
            long preallocAllocations = MeasureWave(new SteppableRunner<WorkTask>("PreallocRunner", TasksPerWave));

            Console.WriteLine("  Allocation comparison - first wave of {0} struct tasks ({1} steps each):", TasksPerWave, StepsPerTask);
            Console.WriteLine();
            Console.WriteLine("    default capacity (3)   : {0,8:N0} bytes   <- buffer growth during the wave", defaultAllocations);
            Console.WriteLine("    preallocated  ({0,3})    : {1,8:N0} bytes", TasksPerWave, preallocAllocations);
            Console.WriteLine();

            //capacity is retained: after a warm-up wave, repeated waves allocate nothing at all
            using (var runner = new SteppableRunner<WorkTask>("SteadyRunner", TasksPerWave))
            {
                RunWave(runner); //warm-up

                GC.Collect();
                GC.WaitForPendingFinalizers();

                long before = GC.GetAllocatedBytesForCurrentThread();

                const int steadyWaves = 5;
                for (int wave = 0; wave < steadyWaves; wave++)
                    RunWave(runner);

                long steadyAllocations = GC.GetAllocatedBytesForCurrentThread() - before;

                Console.WriteLine("  Steady state - {0} more waves on the same runner:", steadyWaves);
                Console.WriteLine();
                Console.WriteLine("    allocations per wave   : {0,8:N0} bytes   <- concrete struct path, no boxing", steadyAllocations / steadyWaves);
            }

            Console.WriteLine();
            Console.WriteLine("  First-wave allocations may include ConcurrentQueue segment growth when the queued");
            Console.WriteLine("  burst exceeds its current segment capacity. After warm-up those segments recycle.");
            Console.WriteLine();

            SafeReadKey();
        }

        static void WarmUpRuntime()
        {
            using var runner = new SteppableRunner<WorkTask>("WarmUpRunner", 1);

            new WorkTask(0).RunOn(runner);

            while (runner.hasTasks)
                runner.Step();
        }

        static long MeasureWave(SteppableRunner<WorkTask> runner)
        {
            using (runner)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                long before = GC.GetAllocatedBytesForCurrentThread();

                RunWave(runner);

                return GC.GetAllocatedBytesForCurrentThread() - before;
            }
        }

        static void RunWave(SteppableRunner<WorkTask> runner)
        {
            Array.Clear(_workDone, 0, _workDone.Length);

            for (int i = 0; i < TasksPerWave; i++)
                new WorkTask(i).RunOn(runner); //concrete struct type is stored and stepped without boxing

            while (runner.hasTasks)
                runner.Step();
        }

        static void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("  ============================================================");
            Console.WriteLine("   Svelto.Tasks Example 04 - Preallocated Runner");
            Console.WriteLine("   Size the runner containers upfront to avoid growth spikes");
            Console.WriteLine("  ============================================================");
            Console.WriteLine();
        }

        static void SafeTitle(string title) { try { Console.Title = title; } catch { } }
        static void SafeReadKey()           { try { Console.ReadKey(); }     catch { } }

        /// <summary>
        /// A struct enumerator paired with SteppableRunner&lt;WorkTask&gt;: the struct-typed RunOn
        /// overload stores and steps it by value, so starting it does not box or allocate an
        /// iterator object. It yields Yield.It for a few steps, then completes by returning false.
        /// IEquatable is implemented so that the runner internal "is this task set?"
        /// check does not box the struct on completion.
        /// </summary>
        struct WorkTask : IEnumerator<TaskContract>, IEquatable<WorkTask>
        {
            public WorkTask(int id) : this()
            {
                _id       = id;
                _stepsLeft = StepsPerTask;
                _valid     = true;
            }

            public bool MoveNext()
            {
                if (_stepsLeft == 0)
                    return false; //task completed

                _workDone[_id]++;
                _stepsLeft--;

                Current = TaskContract.Yield.It; //resume on the next Step()
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

            readonly int _id;
            readonly bool _valid;
            int _stepsLeft;
        }
    }
}

