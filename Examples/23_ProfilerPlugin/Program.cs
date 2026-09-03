using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;

#if TASKS_PROFILER_ENABLED
using Svelto.Tasks.Lean;
using Svelto.Tasks.Profiler;
#endif

#pragma warning disable CS0436

namespace Example23_ProfilerPlugin
{
    static class Program
    {
        const int Frames = 60;

        static void Main()
        {
            try { Console.Title = "Svelto.Tasks — Profiler Plugin"; } catch { }

#if TASKS_PROFILER_ENABLED
            RunMeasured();
#else
            RunWithInstructions();
#endif
        }

#if TASKS_PROFILER_ENABLED

        // ─────────────────────────────────────────────────────────────────────
        //  The plugin: any ITaskProfilerDriver can receive the balanced
        //  runner/task scopes. Unity ships UnityTaskProfilerDriver, which
        //  installs itself automatically and bridges to ProfilerMarkers.
        //  On plain .NET you write your own — this one aggregates per-task
        //  step durations and renders a console report.
        // ─────────────────────────────────────────────────────────────────────
        sealed class ConsoleProfilerDriver : ITaskProfilerDriver
        {
            //EndTask can arrive from several worker threads at once: the driver
            //must be thread-safe, the scheduler does not serialize the callbacks.
            sealed class Stats
            {
                internal double TotalMs;
                internal double MinMs = double.MaxValue;
                internal double MaxMs;
                internal long   Steps;
            }

            readonly object _lock = new object();
            readonly Dictionary<string, Stats> _stats = new Dictionary<string, Stats>();
            long _runnerPasses;

            public void BeginRunner(string runnerName) { }

            public void EndRunner(string runnerName)
            {
                Interlocked.Increment(ref _runnerPasses);
            }

            public void BeginTask(string runnerName, string taskName) { }

            public void EndTask(string runnerName, string taskName, float elapsedMilliseconds)
            {
                lock (_lock)
                {
                    //TaskProfiler.NormalizeTaskName is internal to the library assembly
                    //(the Unity driver compiles into it): for clean rows this plugin
                    //shortens nested names itself
                    var key = string.Concat(Shorten(taskName), "  [", runnerName, "]");

                    if (_stats.TryGetValue(key, out var stats) == false)
                        _stats[key] = stats = new Stats();

                    stats.TotalMs += elapsedMilliseconds;
                    stats.Steps++;

                    if (elapsedMilliseconds < stats.MinMs) stats.MinMs = elapsedMilliseconds;
                    if (elapsedMilliseconds > stats.MaxMs) stats.MaxMs = elapsedMilliseconds;
                }
            }

            internal void PrintReport(int runnerPasses)
            {
                Console.WriteLine("  ┌──────────────────────────────────────────────────────────────────┐");
                Console.WriteLine("  │  📈 ConsoleProfilerDriver — per single task step                  │");
                Console.WriteLine("  ├──────────────────────────────────────────────────────────────────┤");
                Console.WriteLine("  │  runner passes measured: {0,4}                                    │", _runnerPasses);
                Console.WriteLine("  ├──────────────────────────────────────┬─────────┬─────────┬───────┤");
                Console.WriteLine("  │  task                                │   avg   │   max   │ steps │");
                Console.WriteLine("  ├──────────────────────────────────────┼─────────┼─────────┼───────┤");

                lock (_lock)
                {
                    foreach (var pair in _stats)
                    {
                        var s = pair.Value;
                        Console.WriteLine("  │  {0,-36} │ {1,6:0.000} │ {2,6:0.000} │ {3,5} │",
                            Truncate(pair.Key, 36), s.TotalMs / s.Steps, s.MaxMs, s.Steps);
                    }
                }

                Console.WriteLine("  └──────────────────────────────────────┴─────────┴─────────┴───────┘");
            }

            static string Truncate(string value, int width)
            {
                return value.Length <= width ? value : value.Substring(0, width - 1) + "…";
            }

            //NormalizeTaskName is internal to the library: this local stand-in keeps
            //"Ns.Program+FastTask" rows readable as "FastTask"
            internal static string Shorten(string taskName)
            {
                int plus = taskName.LastIndexOf('+');
                return plus >= 0 ? taskName.Substring(plus + 1) : taskName;
            }
        }

        static void RunMeasured()
        {
            PrintBanner();

            //1. install the plugin — from this moment every task step on every
            //   runner (main thread or worker) is funneled through the driver
            var driver = new ConsoleProfilerDriver();
            TaskProfiler.Driver = driver;

            var builtIn = new TaskInfo[0];

            using (var runner = new SteppableRunner("MeasuredLoop"))
            using (var bgRunner = new MultiThreadRunner("MeasuredWorker"))
            {
                new FastTask().RunOn(runner);
                new CpuTask().RunOn(runner);
                new HeavyTask().RunOn(runner);

                new BackgroundTask().RunOn(bgRunner);

                for (int frame = 0; frame < Frames; frame++)
                    runner.Step();

                bgRunner.WaitForTasksDone(5000);

                //2. the built-in aggregate: per-pass min/avg/max kept by the
                //   scheduler itself (independent of the driver plugin)
                TaskProfiler.CopyAndUpdate(ref builtIn);
            }

            Console.WriteLine("  ┌──────────────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │  📈 TaskProfiler built-in stats — per runner pass (last {0} frames)  │", Frames);
            Console.WriteLine("  ├──────────────────────────────────────┬─────────┬─────────┬───────┤");
            Console.WriteLine("  │  task                                │   avg   │   max   │ calls │");
            Console.WriteLine("  ├──────────────────────────────────────┼─────────┼─────────┼───────┤");

            foreach (var info in builtIn)
                Console.WriteLine("  │  {0,-36} │ {1,6:0.000} │ {2,6:0.000} │ {3,5} │",
                    Truncate(ConsoleProfilerDriver.Shorten(info.taskName), 36), info.averageUpdateDuration,
                    info.maxUpdateDuration, info.deltaCalls);

            Console.WriteLine("  └──────────────────────────────────────┴─────────┴─────────┴───────┘");
            Console.WriteLine();

            driver.PrintReport(Frames);

            Console.WriteLine();
            Console.WriteLine("  💡 FastTask ~µs, CpuTask ~0.3ms, HeavyTask ~2ms per step.          ");
            Console.WriteLine("     The bg task's EndTask arrived on the worker thread: the        ");
            Console.WriteLine("     driver had to be thread-safe. This is a measurement build      ");
            Console.WriteLine("     (TASKS_PROFILER_ENABLED adds a lock + stopwatch per step).     ");
            Console.WriteLine();
            Console.WriteLine("  ✅ Done. Press any key to exit.");
            try { Console.CursorVisible = true; } catch { }
        }

        //Windows Thread.Sleep quantizes to ~15.6ms, which would flood the report with
        //granularity noise: the "costly" tasks burn CPU for a controlled duration instead
        static void BurnCpu(double targetMs)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.Elapsed.TotalMilliseconds < targetMs) { }
        }

        sealed class FastTask : IEnumerator<TaskContract>
        {
            int _left = Frames;

            public bool MoveNext()
            {
                if (_left-- == 0) return false;
                Current = TaskContract.Yield.It;
                return true;
            }

            public TaskContract Current { get; private set; }
            object IEnumerator.Current => Current;
            public void Reset() { }
            public void Dispose() { }
        }

        sealed class CpuTask : IEnumerator<TaskContract>
        {
            int _left = Frames;

            public bool MoveNext()
            {
                if (_left == 0) return false;

                BurnCpu(0.3);
                _left--;
                Current = TaskContract.Yield.It;
                return true;
            }

            public TaskContract Current { get; private set; }
            object IEnumerator.Current => Current;
            public void Reset() { }
            public void Dispose() { }
        }

        sealed class HeavyTask : IEnumerator<TaskContract>
        {
            int _left = Frames;

            public bool MoveNext()
            {
                if (_left == 0) return false;

                BurnCpu(2.0);
                _left--;
                Current = TaskContract.Yield.It;
                return true;
            }

            public Svelto.Tasks.TaskContract Current { get; private set; }
            object IEnumerator.Current => Current;
            public void Reset() { }
            public void Dispose() { }
        }

        sealed class BackgroundTask : IEnumerator<TaskContract>
        {
            int _left = 20;

            public bool MoveNext()
            {
                if (_left == 0) return false;

                BurnCpu(2.0);
                _left--;
                Current = TaskContract.Yield.It;
                return true;
            }

            public TaskContract Current { get; private set; }
            object IEnumerator.Current => Current;
            public void Reset() { }
            public void Dispose() { }
        }

        static string Truncate(string value, int width)
        {
            return value.Length <= width ? value : value.Substring(0, width - 1) + "…";
        }

        static void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("  ╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║   📈 Svelto.Tasks Example 23 — Profiler Plugin                 ║");
            Console.WriteLine("  ║   ITaskProfilerDriver: plug any backend into the scheduler     ║");
            Console.WriteLine("  ╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }

#else

        static void RunWithInstructions()
        {
            Console.WriteLine();
            Console.WriteLine("  The task profiler is compiled out of Release builds of Svelto.Tasks");
            Console.WriteLine("  by default (zero-cost when off). Debug builds are instrumented");
            Console.WriteLine("  automatically, so a plain IDE run (F5) measures. To measure a");
            Console.WriteLine("  Release build, rebuild with the opt-in flag:");
            Console.WriteLine();
            Console.WriteLine("    dotnet build Packages/com.sebaslab.svelto.tasks/Svelto.Tasks/Svelto.Tasks.csproj -c Release -p:EnableTasksProfiler=true");
            Console.WriteLine("    dotnet run --project Examples/23_ProfilerPlugin -c Release -p:EnableTasksProfiler=true");
            Console.WriteLine();
            Console.WriteLine("  The flag defines TASKS_PROFILER_ENABLED in both the library and");
            Console.WriteLine("  this example (the define is baked in at compile time, so toggle");
            Console.WriteLine("  it only together with a rebuild).");
            Console.WriteLine();
        }

#endif
    }
}
