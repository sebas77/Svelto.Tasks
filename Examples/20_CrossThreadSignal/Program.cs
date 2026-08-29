using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Lean;

#pragma warning disable

class Program
{
    class BackgroundWorkSignal : WaitForSignal<BackgroundWorkSignal>
    {
        public const int TimeoutMs = 5000;

        public BackgroundWorkSignal(string name) : base(name, timeout: TimeoutMs) { }
    }

    static int BackgroundSignalTimeoutMs => BackgroundWorkSignal.TimeoutMs;

    static BackgroundWorkSignal _signal;
    static volatile int _workProgress;
    static volatile bool _signalReceived;

    static void Main()
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch {}
        try { Console.CursorVisible = false; } catch {}

        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                CROSS-THREAD SIGNAL                         │");
        Console.WriteLine("│           WaitForSignal — Thread Synchronization            │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Scenario: A background thread does work and signals the main");
        Console.WriteLine("  thread when ready. The main thread yields the .Wait() enumerator");
        Console.WriteLine("  on a SteppableRunner, ticking until the signal fires.");
        Console.WriteLine();

        _signal = new BackgroundWorkSignal("BgWorkSignal");
        _workProgress = 0;
        _signalReceived = false;

        var mainRunner = new SteppableRunner("MainThreadRunner");
        var bgRunner = new MultiThreadRunner("BgWorkerRunner");

        Console.WriteLine("  ┌──────────────────────────┬──────────────────────────┐");
        Console.WriteLine("  │   [MAIN THREAD]          │   [BG THREAD]            │");
        Console.WriteLine("  │   waiting... zZz         │   working... ████        │");
        Console.WriteLine("  └──────────────────────────┴──────────────────────────┘");
        Console.WriteLine();

        IEnumerator<TaskContract> MainWaitsForSignal()
        {
            yield return TaskContract.Yield.It;

            var wait = _signal.Wait();
            while (wait.MoveNext())
                yield return TaskContract.Yield.It;

            _signalReceived = true;
        }

        IEnumerator<TaskContract> BackgroundWork()
        {
            for (int i = 0; i <= 100; i += 10)
            {
                _workProgress = i;
                Thread.Sleep(15);
                yield return TaskContract.Yield.It;
            }

            _signal.Signal();
            yield break;
        }

        MainWaitsForSignal().RunOn(mainRunner);
        BackgroundWork().RunOn(bgRunner);

        var spinners = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        const int barWidth = 20;
        int frame = 0;

        //WaitForSignal throws WaitForSignalException (internal) from MoveNext once the
        //configured timeout expires, which would fault the waiting task mid-Step. To keep
        //this demo's outcome explicit we stop driving the runner at the same deadline and
        //report the timeout ourselves instead of letting the exception surface.
        var waitDeadline = DateTime.UtcNow.AddMilliseconds(BackgroundSignalTimeoutMs);

        while (!_signalReceived && mainRunner.hasTasks)
        {
            if (DateTime.UtcNow > waitDeadline)
                break;

            mainRunner.Step();

            int prog = _workProgress;
            string mainSpinner = spinners[frame % spinners.Length];
            int bgFilled = prog * barWidth / 100;
            string bgBar = new string('█', bgFilled) + new string('░', barWidth - bgFilled);

            string mainState = _signalReceived
                ? "✓ RECEIVED SIGNAL! 🔔"
                : $"waiting... {mainSpinner} zZz";

            Console.Write("\r  │ {0,-24}│  working [{1}] {2,3}%  ", mainState, bgBar, prog);
            Console.Write("  step #{0,4}", frame);

            frame++;
            Thread.Sleep(20);
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("  ┌──────────────────────────┬──────────────────────────┐");
        Console.WriteLine("  │   [MAIN THREAD]          │   [BG THREAD]            │");
        Console.WriteLine("  │   {0,-24}│  done [{1}] {2,3}%",
            _signalReceived ? "✓ RECEIVED SIGNAL! 🔔" : "❌ WAIT TIMED OUT",
            new string('█', barWidth), _workProgress);
        Console.WriteLine("  └──────────────────────────┴──────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Main thread finished waiting after {0} steps.", frame);
        Console.WriteLine("  BG work progress: {0}%  Signal fired: {1}", _workProgress,
            _signalReceived ? "✓" : "✗ (timeout)");
        Console.WriteLine();

        Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine(_signalReceived
            ? "  ║  🔔  Cross-thread signal received!                         ║"
            : "  ║  ⚠  WaitForSignal timed out before Signal() arrived.      ║");
        Console.WriteLine("  ║  Background thread → Signal() → Main thread .Wait() done   ║");
        Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("  ┌─ How it worked ────────────────────────────────────────────┐");
        Console.WriteLine("  │                                                            │");
        Console.WriteLine("  │  1. BackgroundWorkSignal subclasses WaitForSignal<T>      │");
        Console.WriteLine("  │     (T : WaitForSignal<T> — self-referential generic)      │");
        Console.WriteLine("  │                                                            │");
        Console.WriteLine("  │  2. BG thread runs on MultiThreadRunner, calls .Signal()   │");
        Console.WriteLine("  │                                                            │");
        Console.WriteLine("  │  3. Main thread yields _signal.Wait() enumerator           │");
        Console.WriteLine("  │     Each MoveNext() checks a volatile bool + timeout        │");
        Console.WriteLine("  │                                                            │");
        Console.WriteLine("  │  4. When BG signals, the volatile bool flips → MoveNext    │");
        Console.WriteLine("  │     returns false → main task continues past the wait     │");
        Console.WriteLine("  │                                                            │");
        Console.WriteLine("  │  5. If the configured timeout expires first, MoveNext      │");
        Console.WriteLine("  │     throws WaitForSignalException and faults the task      │");
        Console.WriteLine("  └────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  ⚠ Gotcha: WaitForSignal<T> is abstract with a self-referential");
        Console.WriteLine("  generic constraint (T : WaitForSignal<T>). This FORCES you to");
        Console.WriteLine("  create a named subclass (e.g. BackgroundWorkSignal) for");
        Console.WriteLine("  readability and debugging — you cannot use it anonymously.");
        Console.WriteLine();
        Console.WriteLine("  Other WaitForSignal features:");
        Console.WriteLine("    • timeout (default 1000ms) — throws WaitForSignalException");
        Console.WriteLine("    • signals auto-reset after completion (the public autoreset argument is currently unused)");
        Console.WriteLine("    • startUnlocked — begins in the signaled state");
        Console.WriteLine("    • SignalBack()/WaitBack() — bidirectional signaling");
        Console.WriteLine();

        try { Console.CursorVisible = true; } catch {}
        mainRunner.Dispose();
        bgRunner.Dispose();
    }
}