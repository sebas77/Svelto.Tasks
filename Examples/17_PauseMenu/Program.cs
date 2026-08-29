using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

#pragma warning disable

class Program
{
    static volatile int _counter;
    static volatile bool _stop;
    static MultiThreadRunner _runner;

    static IEnumerator<TaskContract> CountingTask()
    {
        int local = 0;
        while (_stop == false)
        {
            local++;
            _counter = local;
            yield return TaskContract.Yield.It;
        }
    }

    static void Main()
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch {}
        try { Console.CursorVisible = false; } catch {}

        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                  PAUSE / RESUME MENU                       │");
        Console.WriteLine("│              MultiThreadRunner Pause & Resume               │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Scenario: A game task runs on a MultiThreadRunner. When the");
        Console.WriteLine("  pause menu opens, the runner freezes; when it closes, tasks");
        Console.WriteLine("  resume exactly where they left off.");
        Console.WriteLine();

        _runner = new MultiThreadRunner("GameRunner");
        CountingTask().RunOn(_runner);
        Thread.Sleep(50);

        var spinners = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        int frame = 0;

        Console.WriteLine("  ┌────────┬────────┬────────┬────────┬────────┬────────┐");
        Console.WriteLine("  │ Task 1 │ Task 2 │ Task 3 │ Task 4 │ Task 5 │ Task 6 │");
        Console.WriteLine("  └────────┴────────┴────────┴────────┴────────┴────────┘");
        Console.WriteLine();

        RunPhase(spinners, ref frame, "RUNNING  ", "▶", "🔥", 25, "Tasks processing normally");

        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │           ⏸  P A U S E   M E N U   O P E N             │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────┘");

        _runner.Pause();
        //Pause() is not an in-flight MoveNext barrier: give the worker one scheduling
        //slot to settle before taking the snapshot we compare while paused.
        Thread.Sleep(20);
        int frozen = _counter;
        bool stayedFrozen = true;
        for (int i = 0; i < 20; i++)
        {
            int live = _counter;
            stayedFrozen &= live == frozen;
            Console.Write("\r  ❄ PAUSED  [{0,4}] ❄ snapshot {1} == live {2} → stable: {3}     ", frozen, frozen, live, live == frozen);
            Thread.Sleep(25);
        }
        Console.WriteLine();
        Console.WriteLine(stayedFrozen
            ? "  ✓ Counter frozen at {0} — did NOT change while paused!"
            : "  ❌ Counter changed while paused — pause is not an immediate in-flight barrier.", frozen);

        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │           ▶  R E S U M E   F R O M   P A U S E          │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────┘");

        _runner.Resume();

        RunPhase(spinners, ref frame, "RESUMED  ", "▶", "🔥", 25, "Tasks resumed — counter climbs again");

        _stop = true;
        bool stoppedCleanly = _runner.WaitForTasksDone(1000);

        Console.WriteLine();
        Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  {0}  Final counter: {1,5}                              ║",
            stoppedCleanly ? "✅" : "⚠️", _counter);
        Console.WriteLine("  ║  Pause froze the task state; Resume continued it.        ║");
        Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("  Pause = freeze task states (they stay in the queue), but does not cancel");
        Console.WriteLine("        a MoveNext already running when Pause() is called.");
        Console.WriteLine("  Stop  = cancel in-flight tasks on their next yield; queued ones run after auto-unstop.");
        Console.WriteLine();

        try { Console.CursorVisible = true; } catch {}
        _runner.Dispose();
    }

    static void RunPhase(string[] spinners, ref int frame, string label, string icon, string heat, int iterations, string note)
    {
        for (int i = 0; i < iterations; i++)
        {
            string spinner = spinners[frame % spinners.Length];
            int f = Environment.TickCount / 30;
            string s1 = spinners[f % spinners.Length];
            string s2 = spinners[(f + 1) % spinners.Length];
            string s3 = spinners[(f + 2) % spinners.Length];
            Console.Write("\r  {0} {1} counter: [{2,4}]  {3}  {4}{5}{6}  ({7} {8} {9} {10} {11} {12})  ",
                icon, label, _counter, heat, s1, s2, s3,
                spinners[(f) % spinners.Length], spinners[(f + 1) % spinners.Length], spinners[(f + 2) % spinners.Length],
                spinners[(f + 3) % spinners.Length], spinners[(f + 4) % spinners.Length], spinners[(f + 5) % spinners.Length]);
            frame++;
            Thread.Sleep(30);
        }
        Console.WriteLine();
        Console.Write("  {0}                                                            ", note);
        Console.WriteLine();
    }
}