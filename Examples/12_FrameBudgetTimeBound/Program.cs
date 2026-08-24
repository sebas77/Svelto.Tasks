using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;

#pragma warning disable CS0436

namespace Example12_FrameBudgetTimeBound
{
    static class Program
    {
        const int NumTasks = 10;
        const int TaskMs = 5;
        const float BudgetMs = 20f;

        static readonly bool[] _ranThisStep = new bool[NumTasks];
        static readonly int[] _runCount = new int[NumTasks];
        static readonly char[] _spin = { '|', '/', '-', '\\' };

        static void SafeClear() { try { Console.Clear(); } catch { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch { } }
        static void SafeTitle(string title) { try { Console.Title = title; } catch { } }
        static void SafeReadKey() { try { Console.ReadKey(); } catch { } }

        static void Main()
        {
            SafeTitle("Svelto.Tasks — TimeBoundFlow Budget");
            SafeCursorVisible(false);

            PrintBanner();

            using var runner = new SteppableRunner("TimeBoundRunner");
            runner.UseFlowModifier(new TimeBoundFlow(BudgetMs));

            for (int i = 0; i < NumTasks; i++)
                WorkTask(i).RunOn(runner);

            int totalSteps = 8;
            for (int step = 0; step < totalSteps; step++)
            {
                Array.Clear(_ranThisStep, 0, NumTasks);

                var sw = Stopwatch.StartNew();
                runner.Step();
                sw.Stop();

                DrawStep(step, totalSteps, (float)sw.Elapsed.TotalMilliseconds);
                Thread.Sleep(400);
            }

            Console.WriteLine();
            Console.WriteLine("  ──────────────────────────────────────────────");
            Console.WriteLine("  📊 Execution count per task after {0} steps:", totalSteps);
            Console.WriteLine("  ──────────────────────────────────────────────");
            for (int i = 0; i < NumTasks; i++)
            {
                int bars = _runCount[i];
                string bar = new string('█', bars) + new string('░', Math.Max(0, 8 - bars));
                Console.WriteLine("    Task-{0:D2}: {1} {2,2}× ({3}ms of work done)",
                    i + 1, bar, _runCount[i], TaskMs * _runCount[i]);
            }

            //honest takeaway: with never-completing tasks, the tail of the list starves
            bool anyStarved = false;
            for (int i = 0; i < NumTasks; i++)
                anyStarved |= _runCount[i] == 0;

            if (anyStarved)
            {
                Console.WriteLine();
                Console.WriteLine("  ⚠  Some tasks never ran! Each tick restarts from the FIRST task, so as");
                Console.WriteLine("     long as the early ones never complete, they consume the whole budget.");
                Console.WriteLine("     Let tasks complete to free their slot, or use StaggeredFlow/");
                Console.WriteLine("     TimeSlicedFlow when fairness across tasks matters more than wall-clock.");
            }

            Console.WriteLine();
            Console.WriteLine("  ✅ Done. Press any key to exit.");
            SafeCursorVisible(true);
            SafeReadKey();
        }

        static IEnumerator<TaskContract> WorkTask(int taskId)
        {
            while (true)
            {
                Thread.Sleep(TaskMs);
                _ranThisStep[taskId] = true;
                _runCount[taskId]++;
                yield return TaskContract.Yield.It;
            }
        }

        static void DrawStep(int step, int totalSteps, float actualMs)
        {
            SafeSetCursor(0, 9);

            string spinner = _spin[step % 4].ToString();
            int ranCount = 0;
            for (int i = 0; i < NumTasks; i++)
                if (_ranThisStep[i]) ranCount++;

            Console.WriteLine("  ┌──────────────────────────────────────────────────────┐  ");
            Console.WriteLine("  │  STEP {0,2}/{1}  {2}  TimeBoundFlow({3}ms)  ~{4}ms/task      │  ",
                step + 1, totalSteps, spinner, (int)BudgetMs, TaskMs);
            Console.WriteLine("  ├──────────────────────────────────────────────────────┤  ");

            Console.Write("  │  Tasks:  ");
            for (int i = 0; i < NumTasks; i++)
            {
                if (_ranThisStep[i])
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("⚙️ ");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("·  ");
                }
            }
            Console.ResetColor();
            Console.WriteLine("              │  ");

            Console.WriteLine("  ├──────────────────────────────────────────────────────┤  ");

            int budgetBars = 20;
            int filled = (int)(actualMs / BudgetMs * budgetBars);
            filled = Math.Min(filled, budgetBars);
            double pct = actualMs / BudgetMs * 100;

            Console.Write("  │  ⏱  Budget [");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(new string('█', filled));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('░', budgetBars - filled));
            Console.ResetColor();
            Console.Write("] {0,5:F1}ms / {1}ms  {2,3:F0}% │  ", actualMs, (int)BudgetMs, pct);
            Console.WriteLine();

            Console.Write("  │  Ran {0}/{1} tasks  ", ranCount, NumTasks);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("(~{0} expected: {1}ms/{2}ms)",
                (int)(BudgetMs / TaskMs), (int)BudgetMs, TaskMs);
            Console.ResetColor();
            Console.WriteLine("         │  ");

            Console.WriteLine("  └──────────────────────────────────────────────────────┘  ");
        }

        static void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║   ⏱  Svelto.Tasks Example 12 — Frame Budget (TimeBound)    ║");
            Console.WriteLine("  ║   10 tasks × 5ms each, 20ms per-frame budget                ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  ⚙️ = ran this tick   · = starved (each tick restarts from task 1)");
            Console.WriteLine();
        }
    }
}