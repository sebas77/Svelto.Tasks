using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

namespace FireAndForgetLogging
{
    static class Program
    {
        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 06 Fire & Forget Logging"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            var runner = new SteppableRunner("TelemetryRunner");

            IEnumerator<TaskContract> Child()
            {
                Step(2, "CHILD ", "[2] telemetry: buffering event...", "▒");
                yield return TaskContract.Yield.It;
                Step(3, "CHILD ", "[3] telemetry: flush to disk done   ", "█");
            }

            IEnumerator<TaskContract> Parent()
            {
                Step(1, "PARENT", "[1] gameplay: player jumped          ", "█");
                yield return Child().Forget();
                Step(4, "PARENT", "[4] gameplay: resume immediately     ", "█");
            }

            Parent().RunOn(runner);

            AnimateThreeSteps(runner);

            SafeSetCursor(0, 22);
            Console.WriteLine(new string(' ', 80));

            //prove what actually happened instead of asserting it: the order list is filled by
            //the tasks themselves while they run
            int[] expected = { 1, 4, 2, 3 };
            bool proofHolds = _order.Count == expected.Length;
            if (proofHolds)
                for (int i = 0; i < expected.Length; i++)
                    proofHolds &= _order[i] == expected[i];

            Console.WriteLine($"  {(proofHolds ? "✅" : "❌")} Recorded execution order: [{string.Join(" → ", _order)}]" +
                              $"{(proofHolds ? "" : $"   (expected [{string.Join(" → ", expected)})]")}");
            Console.WriteLine("  💡 Forget() fired the child on the SAME runner, parent kept going on the next step.");
            Console.WriteLine();
            runner.Dispose();
        }

        static void AnimateThreeSteps(SteppableRunner runner)
        {
            const int spinFrames = 4;
            var spinner = new[] { '|', '/', '─', '\\' };
            int frame = 0;

            for (int i = 0; i < 3; i++)
            {
                for (int s = 0; s < spinFrames; s++)
                {
                    SafeSetCursor(0, 17);
                    Console.WriteLine($"  ⚙  Stepping runner...  {spinner[frame]}  step {i + 1}/3");
                    frame = (frame + 1) % spinner.Length;
                    Thread.Sleep(180);
                }
                runner.Step();
            }

            SafeSetCursor(0, 17);
            Console.WriteLine("  ⚙  Stepping runner...  ✓  done (3 steps)          ");
        }

        static int _row = 7;
        static readonly List<int> _order = new List<int>();

        static void Step(int n, string who, string msg, string bar)
        {
            _order.Add(n);
            SafeSetCursor(0, _row);
            Console.WriteLine($"  {who} │ {msg} {bar}");
            _row++;
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  06 · FIRE & FORGET LOGGING  ·  .Forget()                   ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  Parent fires a telemetry child but does NOT wait for it.    ║");
            Console.WriteLine("  ║  The child runs on the same runner, in the background.       ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("   TIMELINE  (two lanes, same runner, stepped manually)");
            Console.WriteLine();
            Console.WriteLine("   PARENT  ─[1]──────────[4]──────────▶");
            Console.WriteLine("               ╲                          ");
            Console.WriteLine("                ╲ Forget()                ");
            Console.WriteLine("                 ▼                        ");
            Console.WriteLine("   CHILD  ───────[2]────[3]─────────▶     ");
            Console.WriteLine();
            Console.WriteLine("   ─────────────────────────────────────");
            Console.WriteLine();
        }
    }
}