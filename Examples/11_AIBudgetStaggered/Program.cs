using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;

#pragma warning disable CS0436

namespace Example11_AIBudgetStaggered
{
    static class Program
    {
        const int NumUnits = 10;
        const int MaxPerFrame = 3;

        static readonly bool[] _activeThisFrame = new bool[NumUnits];
        static readonly int[] _thinkCount = new int[NumUnits];
        static readonly char[] _spin = { '|', '/', '-', '\\' };

        static void SafeClear() { try { Console.Clear(); } catch { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch { } }
        static void SafeTitle(string title) { try { Console.Title = title; } catch { } }

        static void Main()
        {
            SafeTitle("Svelto.Tasks — StaggeredFlow AI Budget");
            SafeCursorVisible(false);

            PrintBanner();

            using var runner = new SteppableRunner("AI-BudgetRunner");
            runner.UseFlowModifier(new StaggeredFlow(MaxPerFrame));

            for (int i = 0; i < NumUnits; i++)
                AITask(i).RunOn(runner);

            int totalFrames = 12;
            for (int frame = 0; frame < totalFrames; frame++)
            {
                Array.Clear(_activeThisFrame, 0, NumUnits);

                runner.Step();

                DrawFrame(frame);
                Thread.Sleep(350);
            }

            Console.WriteLine();
            Console.WriteLine("  ──────────────────────────────────────────────");
            Console.WriteLine("  📊 Total think-ticks per unit after {0} frames:", totalFrames);
            Console.WriteLine("  ──────────────────────────────────────────────");
            for (int i = 0; i < NumUnits; i++)
                Console.WriteLine("    🤖 A{0:D2}: thought {1,3} times", i + 1, _thinkCount[i]);

            Console.WriteLine();
            Console.WriteLine("  ✅ Done. Press any key to exit.");
            SafeCursorVisible(true);
        }

        static IEnumerator<TaskContract> AITask(int unitId)
        {
            while (true)
            {
                _activeThisFrame[unitId] = true;
                _thinkCount[unitId]++;
                yield return TaskContract.Yield.It;
            }
        }

        static void DrawFrame(int frame)
        {
            SafeSetCursor(0, 9);
            string spinner = _spin[frame % 4].ToString();

            Console.WriteLine("  ┌──────────────────────────────────────────────────┐  ");
            Console.WriteLine("  │  FRAME {0,3} / 12   {1}   StaggeredFlow(3) — max 3 think/frame │  ", frame + 1, spinner);
            Console.WriteLine("  ├──────────────────────────────────────────────────┤  ");

            Console.Write("  │  ");
            for (int i = 0; i < NumUnits; i++)
            {
                if (_activeThisFrame[i])
                    Console.Write("🤖 ");
                else
                    Console.Write("💤 ");
            }
            Console.WriteLine("   │  ");

            Console.Write("  │  ");
            for (int i = 0; i < NumUnits; i++)
            {
                if (_activeThisFrame[i])
                    Console.Write("[A{0:D2}]", i + 1);
                else
                    Console.Write(" .... ");
            }
            Console.WriteLine(" │  ");

            int activeCount = 0;
            for (int i = 0; i < NumUnits; i++)
                if (_activeThisFrame[i]) activeCount++;

            Console.Write("  │  Active: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("{0}/3", activeCount);
            Console.ResetColor();
            Console.WriteLine("  Idle: {0}/10          │  ", NumUnits - activeCount);

            Console.Write("  │  Budget: [");
            for (int i = 0; i < MaxPerFrame; i++)
            {
                if (i < activeCount)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("██████");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("░░░░░░");
                }
            }
            Console.ResetColor();
            Console.WriteLine("] {0}/{1}    │", activeCount, MaxPerFrame);

            Console.WriteLine("  └──────────────────────────────────────────────────┘  ");
        }

        static void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║   ⚡ Svelto.Tasks Example 11 — AI Budget (Staggered)    ║");
            Console.WriteLine("  ║   10 AI units, max 3 think per frame (first 3 win)     ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  🤖 = thinking this frame   💤 = idle / starved");
            Console.WriteLine();
        }
    }
}