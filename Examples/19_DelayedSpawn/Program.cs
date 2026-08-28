using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Lean;

#pragma warning disable

class Program
{
    static bool _spawned;
    static SteppableRunner _runner;

    static IEnumerator<TaskContract> SpawnAfterDelay(float seconds)
    {
        var wait = new WaitForSecondsEnumerator(seconds);
        while (wait.MoveNext())
            yield return TaskContract.Yield.It;

        _spawned = true;
    }

    static void Main()
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch {}
        try { Console.CursorVisible = false; } catch {}

        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                  DELAYED SPAWN                             │");
        Console.WriteLine("│            WaitForSecondsEnumerator Countdown                │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Scenario: A level starts, then 2 seconds later an enemy spawns.");
        Console.WriteLine("  We run a WaitForSecondsEnumerator on a SteppableRunner and tick");
        Console.WriteLine("  it manually, showing a live countdown to the spawn event.");
        Console.WriteLine();

        _runner = new SteppableRunner("SpawnRunner");
        _spawned = false;

        const float delaySeconds = 2.0f;
        var task = SpawnAfterDelay(delaySeconds);
        task.RunOn(_runner);

        var sw = Stopwatch.StartNew();
        var spinners = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        const int barWidth = 40;
        int frame = 0;

        while (!_spawned && _runner.hasTasks)
        {
            _runner.Step();

            double elapsed = sw.Elapsed.TotalSeconds;
            double remaining = Math.Max(0, delaySeconds - elapsed);
            double progress = Math.Min(1.0, elapsed / delaySeconds);

            int filled = (int)(progress * barWidth);
            string bar = new string('█', filled) + new string('░', barWidth - filled);
            string spinner = spinners[frame % spinners.Length];

            Console.Write("\r  {0} Step #{1,4}  Spawn in: {2:0.0}s  [{3}] {4,3:0}%  ",
                spinner, frame, remaining, bar, progress * 100);

            frame++;
            Thread.Sleep(20);
        }

        sw.Stop();

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("        ✸   ✺   ✹       ");
        Console.WriteLine("          ✴ SPAWN ✴      ");
        Console.WriteLine("        ✸  👾 ENEMY  ✺   ");
        Console.WriteLine("          ✹   ✸   ✺      ");
        Console.WriteLine();

        Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  👾  ENEMY SPAWNED after 2.0s!                              ║");
        Console.WriteLine("  ║  WaitForSecondsEnumerator completed → spawn triggered     ║");
        Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("  Runner finished after {0} steps in {1:0.0}s", frame, sw.Elapsed.TotalSeconds);
        Console.WriteLine();
        Console.WriteLine("  ┌─ How it worked ────────────────────────────────────────────┐");
        Console.WriteLine("  │ var wait = new WaitForSecondsEnumerator(2.0f);             │");
        Console.WriteLine("  │ while (wait.MoveNext())                                    │");
        Console.WriteLine("  │     yield return TaskContract.Yield.It;                    │");
        Console.WriteLine("  │                                                            │");
        Console.WriteLine("  │ The enumerator tracks a target time (DateTime.UtcNow + 2s) │");
        Console.WriteLine("  │ and keeps returning true (yielding) until the time passes. │");
        Console.WriteLine("  │ Then MoveNext() returns false → the task continues.         │");
        Console.WriteLine("  └────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  ⚠ Gotcha: For ZERO allocation, use ReusableWaitForSecondsEnumerator");
        Console.WriteLine("  (a struct) instead of WaitForSecondsEnumerator (a class). The");
        Console.WriteLine("  struct can be Reset() and reused — no GC pressure.");
        Console.WriteLine();
        Console.WriteLine("  Example with the reusable struct:");
        Console.WriteLine("    var wait = new ReusableWaitForSecondsEnumerator(2.0f);");
        Console.WriteLine("    wait.Reset();     // reuse with the same duration");
        Console.WriteLine("    wait.Reset(5.0f); // or reuse with a new duration");
        Console.WriteLine();

        try { Console.CursorVisible = true; } catch {}
        _runner.Dispose();
    }
}