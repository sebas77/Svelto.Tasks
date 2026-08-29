#pragma warning disable CA1822, CA1852, IDE0060
using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

namespace GameLoop
{
    static class Program
    {
        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 01 Game Loop"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            int frameCount = 0;

            IEnumerator<TaskContract> FrameCounterTask()
            {
                for (int i = 1; i <= 10; i++)
                {
                    frameCount = i;
                    yield return TaskContract.Yield.It;
                }
            }

            using (var runner = new SteppableRunner("GameLoopRunner"))
            {
                FrameCounterTask().RunOn(runner);

                var spinner = new[] { '|', '/', '─', '\\' };
                int spinIdx = 0;

                while (runner.hasTasks)
                {
                    DrawGear(spinIdx, frameCount);
                    runner.Step();
                    spinIdx = (spinIdx + 1) % spinner.Length;
                    Thread.Sleep(200);
                }

                DrawGear(0, frameCount, done: true);
            }

            SafeSetCursor(0, 16);
            Console.WriteLine("  ┌──────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │  ✅ Task complete! Counted to 10 across 11 Step() calls. │");
            Console.WriteLine("  │  💡 The final Step() observes completion and drains the   │");
            Console.WriteLine("  │     runner, so completion detection costs one extra step. │");
            Console.WriteLine("  └──────────────────────────────────────────────────────────┘");
            Console.WriteLine();
        }

        static void DrawGear(int spinIdx, int frameCount, bool done = false)
        {
            var spinner = new[] { '|', '/', '─', '\\' };
            int total = 10;
            int filled = done ? total : Math.Min(frameCount, total);
            string bar = new string('█', filled) + new string('░', total - filled);

            SafeSetCursor(0, 12);
            Console.WriteLine("  ╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  🎮 GAME LOOP — SteppableRunner ticking each frame             ║");
            Console.WriteLine("  ╠═══════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"  ║                                                                 ║");
            Console.WriteLine($"  ║   ⚙  {spinner[spinIdx]}   Frame {frameCount,2}/{total}   [{bar}]  {(done ? "✓ DONE" : "     ")}       ║");
            Console.WriteLine($"  ║                                                                 ║");
            Console.WriteLine("  ╚═══════════════════════════════════════════════════════════════╝");
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  01 · GAME LOOP  ·  Lean Task + SteppableRunner             ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  A simulated game loop ticks a SteppableRunner each frame.   ║");
            Console.WriteLine("  ║  A task counts to 10, yielding TaskContract.Yield.It between ║");
            Console.WriteLine("  ║  each count. The spinner shows the runner being stepped.     ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("   ┌─────────┐     ┌──────────┐     ┌─────────────┐");
            Console.WriteLine("   │  Main   │────▶│ Step()   │────▶│  Task:      │");
            Console.WriteLine("   │  Loop   │     │ tick #N  │     │  count++    │");
            Console.WriteLine("   │  200ms  │◀────│ yield    │◀────│  Yield.It   │");
            Console.WriteLine("   └─────────┘     └──────────┘     └─────────────┘");
            Console.WriteLine();
        }
    }
}