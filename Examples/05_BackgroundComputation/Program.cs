#pragma warning disable CA1822, CA1852, IDE0060
using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Lean;

namespace BackgroundComputation
{
    static class Program
    {
        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        static volatile int _bgProgress;
        static volatile int _bgResult = -1;
        static volatile bool _bgDone;

        static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 05 Background Computation"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            using (var bgRunner = new MultiThreadRunner("BgComputeRunner"))
            {
                IEnumerator<TaskContract> HeavyComputation()
                {
                    int sum = 0;
                    int total = 20;
                    for (int i = 1; i <= total; i++)
                    {
                        sum += i * i;
                        _bgProgress = i * 100 / total;
                        Thread.Sleep(80);
                        yield return TaskContract.Yield.It;
                    }
                    _bgResult = sum;
                    _bgDone = true;
                }

                Continuation cont = HeavyComputation().RunOn(bgRunner);

                var spinner = new[] { '|', '/', '─', '\\' };
                int spinIdx = 0;

                while (cont.isRunning)
                {
                    DrawPanels(spinner[spinIdx], _bgProgress, _bgDone, _bgResult);
                    spinIdx = (spinIdx + 1) % spinner.Length;
                    Thread.Sleep(100);
                }

                DrawPanels('✓', 100, true, _bgResult, final: true);
            }

            SafeSetCursor(0, 20);
            Console.WriteLine("  ┌──────────────────────────────────────────────────────────────┐");
            Console.WriteLine($"  │  ✅ Background task complete! Result = Σ(i²) for i=1..20 = {_bgResult} │");
            Console.WriteLine("  │  💡 Main thread polled Continuation.isRunning while bg ran.   │");
            Console.WriteLine("  │  💡 MultiThreadRunner.Dispose() stops the background thread.  │");
            Console.WriteLine("  └──────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
        }

        static void DrawPanels(char spinner, int bgPct, bool bgDone, int bgResult, bool final = false)
        {
            int barLen = 16;
            int filled = bgPct * barLen / 100;
            string bar = new string('█', filled) + new string('░', barLen - filled);

            SafeSetCursor(0, 12);
            Console.WriteLine("  ┌──────────────────────────────┐  ┌──────────────────────────────┐");
            Console.WriteLine("  │  🧵 MAIN THREAD              │  │  ⚙  BG THREAD                │");
            Console.WriteLine("  │                              │  │                              │");
            Console.WriteLine($"  │   spinner:  {spinner}               │  │   progress: [{bar}] {bgPct,3}% │");
            Console.WriteLine("  │   polling cont.isRunning...  │  │   computing Σ(i²) ...        │");
            Console.WriteLine($"  │   status: {(final ? "✓ done    " : "⏳ waiting ")}        │  │   status: {(bgDone ? "✓ done    " : "⏳ running ")}        │");
            Console.WriteLine("  │                              │  │                              │");
            Console.WriteLine("  └──────────────────────────────┘  └──────────────────────────────┘");
            Console.WriteLine();
            if (final)
            {
                Console.WriteLine($"   📊 Result received on main thread:  Σ(i²) = {bgResult}    ✅");
            }
            else
            {
                Console.WriteLine("   📊 Waiting for background task to publish its result...");
            }
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  05 · BACKGROUND COMPUTATION  ·  RunOn + MultiThreadRunner  ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  Heavy work runs on a background MultiThreadRunner thread.   ║");
            Console.WriteLine("  ║  Main thread shows a spinner and polls Continuation.isRunning║");
            Console.WriteLine("  ║  until the bg task finishes, then prints the result.         ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("   ┌──────────────┐  RunOn(bg)   ┌──────────────┐");
            Console.WriteLine("   │  MAIN THREAD │ ───────────▶ │  BG THREAD   │");
            Console.WriteLine("   │  spinner     │              │  Σ(i²) ...   │");
            Console.WriteLine("   │  poll isRun  │ ◀─────────── │  yields      │");
            Console.WriteLine("   └──────────────┘  Continuation └──────────────┘");
            Console.WriteLine();
        }
    }
}