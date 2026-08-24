#pragma warning disable CA1822, CA1852, IDE0060
using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

namespace ContinueChildTask
{
    static class Program
    {
        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        static int _stageRow = 12;

        static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 04 Continue Child Task"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            int childCounter = 0;
            int parentResult = 0;

            IEnumerator<TaskContract> ChildTask()
            {
                LogStage("CHILD ", "▶ start", "▶");
                yield return TaskContract.Yield.It;
                childCounter++;
                LogStage("CHILD ", $"  working... counter={childCounter}", "▓");
                yield return TaskContract.Yield.It;
                childCounter++;
                LogStage("CHILD ", $"  working... counter={childCounter}", "▓");
                yield return TaskContract.Yield.It;
                childCounter++;
                LogStage("CHILD ", "◀ done   counter=" + childCounter, "█");
            }

            IEnumerator<TaskContract> ParentTask()
            {
                LogStage("PARENT", "▶ start — delegating to child", "▶");
                yield return ChildTask().Continue();
                parentResult = childCounter * 10;
                LogStage("PARENT", "◀ resumed — result = " + parentResult, "█");
            }

            using (var runner = new SteppableRunner("ContinueRunner"))
            {
                ParentTask().RunOn(runner);

                int step = 0;
                while (runner.hasTasks)
                {
                    step++;
                    SafeSetCursor(0, 10);
                    Console.WriteLine($"  ⚙  Stepping runner...  step {step}   runner.hasTasks = true ");
                    runner.Step();
                    Thread.Sleep(450);
                }

                SafeSetCursor(0, 10);
                Console.WriteLine($"  ⚙  Stepping runner...  done ({step} steps)   runner.hasTasks = false");
            }

            SafeSetCursor(0, _stageRow + 8);
            Console.WriteLine("  ┌──────────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │  ✅ Parent delegated to child via .Continue() and waited.     │");
            Console.WriteLine($"  │  💡 child counter={childCounter} → parent result={parentResult}                     │");
            Console.WriteLine("  │  💡 .Continue() runs child on the SAME runner; parent waits.  │");
            Console.WriteLine("  └──────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
        }

        static void LogStage(string who, string msg, string bar)
        {
            SafeSetCursor(0, _stageRow);
            Console.WriteLine($"  {who} │ {msg,-40} {bar}");
            _stageRow++;
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  04 · CONTINUE CHILD TASK  ·  .Continue() on same runner    ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  A parent yields ChildTask().Continue(). The child runs on   ║");
            Console.WriteLine("  ║  the SAME runner; the parent suspends until the child done.  ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("    ┌─────────┐         .Continue()         ┌─────────┐");
            Console.WriteLine("    │ PARENT  │ ───────────────────────▶    │ CHILD   │");
            Console.WriteLine("    │ (wait)  │                              │ (runs)  │");
            Console.WriteLine("    │         │ ◀───────────────────────     │ done    │");
            Console.WriteLine("    └─────────┘         resumes              └─────────┘");
            Console.WriteLine();
            Console.WriteLine("   ─────────────────────────────────────────────────────────");
            Console.WriteLine();
        }
    }
}