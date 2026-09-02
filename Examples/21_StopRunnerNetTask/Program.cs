using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Svelto.Tasks.Lean;

#pragma warning disable

class Program
{
    static volatile bool _stop;
    static volatile int  _iterations;
    static volatile int  _hostedThreadId;
    static          bool _completedNaturally; // only set if the loop is allowed to finish

    // hosted on the runner through the synchronization context: every await comes back to it
    static async Task HostedJob()
    {
        await Task.Yield(); // hop onto the runner thread

        _hostedThreadId = Thread.CurrentThread.ManagedThreadId;

        while (_stop == false)
        {
            _iterations++;
            await Task.Yield();
        }

        _completedNaturally = true; // reached only if the loop is ever allowed to end
    }

    static void Main()
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch {}
        try { Console.CursorVisible = false; } catch {}

        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│         .NET TASKS HOSTED ON A SVELTO RUNNER               │");
        Console.WriteLine("│      TaskSynchronizationContext lifecycle proof            │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  An async method runs under a TaskSynchronizationContext");
        Console.WriteLine("  attached to a MultiThreadRunner: every await continuation");
        Console.WriteLine("  resumes on the runner's thread. Disposing the runner freezes");
        Console.WriteLine("  the task forever, and the state machine becomes collectable.");
        Console.WriteLine();

        // the runner is kept referenced by Main for the whole demo (a disposed runner
        // holds nothing, but keeping it avoids its finalizer warning mid-demo)
        var runner = new MultiThreadRunner("BgWorker");

        var jobRef = RunHostedScenario(runner);

        // ── Phase 3: garbage collection ───────────────────────────────────
        Console.WriteLine("  [3] COLLECTED: scenario method returned — every reference to");
        Console.WriteLine("      the task handle and the context is gone. Forcing GC...");
        Console.WriteLine();

        bool collected = false;
        for (int attempt = 0; attempt < 5 && collected == false; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            collected = jobRef.IsAlive == false;

            Console.WriteLine("      GC attempt {0}: WeakReference.IsAlive = {1}", attempt,
                jobRef.IsAlive ? "true ⚠️ (still rooted)" : "false ✅ (collected)");
            Thread.Sleep(50);
        }
        Console.WriteLine();
        Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  {0}  ║",
            collected
                ? "✅  HOSTED TASK RAN ON RUNNER, FROZE AND WAS COLLECTED   "
                : "⚠️  FROZE OK, BUT STATE MACHINE STILL ROOTED AFTER GC    ");
        Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("  The context hosts .NET async methods so that every await");
        Console.WriteLine("  continuation resumes on the Svelto runner. Stopping the");
        Console.WriteLine("  runner abandons them: no resumption, no notification, and");
        Console.WriteLine("  no reference held once you drop yours. Release hosted work");
        Console.WriteLine("  from narrow scopes so abandonment becomes real reclamation.");
        Console.WriteLine("  (GC forced here just for demonstration.)");
        Console.WriteLine();

        try { Console.CursorVisible = true; } catch {}

        runner.Dispose(); // already disposed inside the scenario; safe no-op
    }

    /// <summary>
    /// Runs the entire hosted scenario in its own frame: when this method returns, the context
    /// (which roots queued continuations through its queues) and the task handle are gone.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference RunHostedScenario(MultiThreadRunner runner)
    {
        var mainThreadId = Thread.CurrentThread.ManagedThreadId;

        var context = new TaskSynchronizationContext(runner);
        var jobTask = context.Run(HostedJob);
        var jobRef  = new WeakReference(jobTask);

        // ── Phase 1: the async body executes ON the runner thread ─────────
        Console.WriteLine("  [1] HOSTED: sampling the loop while the runner is alive");
        var sw = Stopwatch.StartNew();
        Thread.Sleep(500);
        int firstSample  = _iterations;
        Thread.Sleep(300);
        int secondSample = _iterations;

        Console.WriteLine("      iterations after {0}ms: {1}", sw.ElapsedMilliseconds, secondSample);
        Console.WriteLine("      grew between samples: {1} → {2}  ({0})",
            secondSample > firstSample ? "YES ✅" : "NO ⚠️ ", firstSample, secondSample);
        Console.WriteLine("      resumed on thread {0}, main thread is {1} → running on {2}",
            _hostedThreadId, mainThreadId,
            _hostedThreadId != mainThreadId ? "the Svelto runner thread" : "?!?");
        Console.WriteLine();

        // ── Phase 2: explicit stop — continuations never execute again ────
        Console.WriteLine("  [2] STOPPED: disposing the runner mid-await...");
        int countAtDisposal = _iterations;

        sw.Restart();
        runner.Dispose(); // pump dies here: posted continuations are never executed anymore

        bool frozen = true;
        for (int check = 0; check < 6; check++)
        {
            Thread.Sleep(250);
            if (_iterations != countAtDisposal)
                frozen = false;
            Console.Write("\r      t+{0,4}ms: iterations = {1}   frozen = {2,-5}   ",
                sw.ElapsedMilliseconds, _iterations, frozen);
        }
        Console.WriteLine();
        Console.WriteLine("      completed naturally: {0}", _completedNaturally ? "YES" : "NO ✅ (frozen mid-await)");
        Console.WriteLine();

        return jobRef; //context and jobTask die with this frame → state machine unrooted
    }
}
