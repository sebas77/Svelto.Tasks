#pragma warning disable CA1822, CA1852, IDE0060
using System;
using System.Collections;
using System.Threading;
using Svelto.Tasks.ExtraLean;

namespace SimpleCoroutine
{
    static class Program
    {
        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 02 Simple Coroutine"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            int countdown = 5;
            bool done = false;

            IEnumerator CountdownTask()
            {
                while (countdown > 0)
                {
                    yield return null;
                    countdown--;
                }
                done = true;
            }

            using (var runner = new SteppableRunner("ExtraLeanRunner"))
            {
                CountdownTask().RunOn(runner);

                while (runner.hasTasks)
                {
                    DrawCountdown(countdown, done);
                    runner.Step();
                    Thread.Sleep(600);
                }

                DrawCountdown(countdown, done, final: true);
            }

            SafeSetCursor(0, 22);
            Console.WriteLine("  ┌──────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │  ✅ Countdown complete! ExtraLean task finished.          │");
            Console.WriteLine("  │  💡 yield return null = wait one Step() (no TaskContract)  │");
            Console.WriteLine("  └──────────────────────────────────────────────────────────┘");
            Console.WriteLine();
        }

        static void DrawCountdown(int n, bool done, bool final = false)
        {
            int display = final ? 0 : n;
            int barLen = 5;
            int filled = done || final ? 0 : Math.Max(0, display);
            string bar = new string('█', filled) + new string('░', barLen - filled);

            SafeSetCursor(0, 12);
            Console.WriteLine("  ╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  🚀 COUNTDOWN — ExtraLean IEnumerator (yield return null)     ║");
            Console.WriteLine("  ╠═══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║                                                               ║");
            Console.WriteLine($"  ║     T-{display}   [{bar}]   {(done || final ? "✓ LIFT OFF!" : "          ")}         ║");
            Console.WriteLine("  ║                                                               ║");
            Console.WriteLine("  ╚═══════════════════════════════════════════════════════════════╝");

            SafeSetCursor(0, 19);
            DrawBigDigit(display);
        }

        static void DrawBigDigit(int d)
        {
            string s = d.ToString();
            string[][] digits =
            {
                new[] { " ██████ ", " ██████ ", " ██████ ", " ██████ ", " ██████ " },
                new[] { "    ███ ", "    ███ ", "    ███ ", "    ███ ", "    ███ " },
                new[] { " ██████ ", "    ███ ", " ██████ ", " ███    ", " ██████ " },
                new[] { " ██████ ", "    ███ ", " ██████ ", "    ███ ", " ██████ " },
                new[] { " ███ ███", " ███ ███", " ██████ ", "    ███ ", "    ███ " },
                new[] { " ██████ ", " ███    ", " ██████ ", "    ███ ", " ██████ " },
            };
            int idx = (d >= 0 && d <= 5) ? d : 0;
            foreach (var row in digits[idx])
                Console.WriteLine($"    {row}");
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  02 · SIMPLE COROUTINE  ·  ExtraLean Task                   ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  A plain IEnumerator countdown from 5 to 0 using             ║");
            Console.WriteLine("  ║  ExtraLean.SteppableRunner. yield return null = wait a step. ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("    ┌───────────────┐    yield null    ┌───────────────┐");
            Console.WriteLine("    │  Steppable    │ ──────────────▶  │   Countdown   │");
            Console.WriteLine("    │  Runner       │                  │   IEnumerator │");
            Console.WriteLine("    │  .Step()      │ ◀──────────────  │   count--     │");
            Console.WriteLine("    └───────────────┘                  └───────────────┘");
            Console.WriteLine();
        }
    }
}