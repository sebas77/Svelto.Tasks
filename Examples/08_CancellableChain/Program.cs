using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;

namespace CancellableChain
{
    static class Program
    {
        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 08 Cancellable Chain"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            //these flags are written by the tasks while they run: the summary printed at
            //the end is derived from them instead of being hardcoded
            bool loadCompleted      = false;
            bool validationFailed   = false;
            bool processReached     = false;
            bool parentFinalReached = false;

            IEnumerator<TaskContract> LoadStep()
            {
                for (int i = 0; i <= 10; i++)
                {
                    Bar(12, "LOAD", i, 10);
                    Thread.Sleep(40);
                }
                loadCompleted = true;
                yield return TaskContract.Yield.It;
                SafeSetCursor(0, 13);
                Console.WriteLine("  └─ LOAD completed");
            }

            IEnumerator<TaskContract> ValidateStep()
            {
                Bar(14, "VALIDATE", 0, 10);
                Thread.Sleep(200);
                for (int i = 0; i <= 4; i++)
                {
                    Bar(14, "VALIDATE", i, 10);
                    Thread.Sleep(80);
                }
                SafeSetCursor(0, 15);
                Console.WriteLine("  └─ ❌ VALIDATION FAILED: checksum mismatch!");
                validationFailed = true;

                //Break.It stops ONLY this task: Chain resumes right after the Continue and
                //gets the chance to forward the failure. Break.AndStop here instead would
                //kill this task's caller too... but nothing above it (see the outro)
                yield return TaskContract.Break.It;
            }

            IEnumerator<TaskContract> ProcessStep()
            {
                processReached = true;
                SafeSetCursor(0, 16);
                Console.WriteLine("  [PROCESS] this should NEVER run");
                yield return TaskContract.Yield.It;
            }

            IEnumerator<TaskContract> Chain()
            {
                yield return LoadStep().Continue();
                yield return ValidateStep().Continue();

                //Break.AndStop propagates exactly ONE level up: had ValidateStep yielded it
                //directly, this task would stop but an outer caller would resume unaware,
                //because a task killed by a child break cannot run forwarding code of its own.
                //To cancel several levels at once, each level must re-yield the break itself:
                if (validationFailed)
                    yield return TaskContract.Break.AndStop; //stops Chain AND Parent

                yield return ProcessStep().Continue();
            }

            IEnumerator<TaskContract> Parent()
            {
                yield return Chain().Continue();
                parentFinalReached = true;
                SafeSetCursor(0, 18);
                Console.WriteLine("  [PARENT] final step reached — this should NEVER happen");
                yield return 42;
            }

            DrawChain();

            Parent().Complete(5000);

            //the expected outcome: LOAD done, PROCESS skipped, PARENT cancelled
            bool chainSnapped = loadCompleted && validationFailed && !processReached && !parentFinalReached;

            SafeSetCursor(0, 20);
            Console.WriteLine("  ╔════════════════════════════════════════════════════════╗");
            Console.WriteLine(chainSnapped
                ? "  ║  💥 CHAIN SNAPPED — cancellation reached every level    ║"
                : "  ║  ⚠  UNEXPECTED EXECUTION — check the report below       ║");
            Console.WriteLine("  ╠════════════════════════════════════════════════════════╣");
            ReportRow("LOAD completed?",    loadCompleted,       "✅ YES",            "❌ NO");
            ReportRow("PROCESS reached?",  !processReached,     "❌ NO (skipped)",   "⚠️ IT RAN!");
            ReportRow("PARENT final?",     !parentFinalReached, "❌ NO (cancelled)", "⚠️ REACHED!");
            Console.WriteLine("  ╚════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            if (chainSnapped)
            {
                Console.WriteLine("  💡 ValidateStep failed with Break.It, then Chain forwarded the failure");
                Console.WriteLine("     with Break.AndStop: PROCESS was skipped AND Parent was cancelled.");
                Console.WriteLine();
                Console.WriteLine("  ⚠  Gotcha: Break.AndStop propagates exactly ONE level up. Had it been");
                Console.WriteLine("     yielded by ValidateStep directly, Chain would stop but Parent would");
                Console.WriteLine("     resume — a killed task cannot forward its own break. To cancel N");
                Console.WriteLine("     levels, each intermediate level must re-yield Break.AndStop.");
            }
            else
            {
                Console.WriteLine("  ⚠  The chain did not snap as expected.");
            }
            Console.WriteLine();
        }

        static void ReportRow(string label, bool asExpected, string whenTrue, string whenFalse)
        {
            Console.Write($"  ║  {label,-21}");
            Console.ForegroundColor = asExpected ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write(asExpected ? whenTrue : whenFalse);
            Console.ResetColor();
            Console.WriteLine();
        }

        static void DrawChain()
        {
            SafeSetCursor(0, 10);
            Console.WriteLine("   ┌────────┐    ┌──────────┐    ┌─────────┐");
            Console.WriteLine("   │  LOAD  │───▶│ VALIDATE │───▶│ PROCESS │");
            Console.WriteLine("   └────────┘    └──────────┘    └─────────┘");
            Console.WriteLine();
        }

        static void Bar(int row, string label, int filled, int total)
        {
            SafeSetCursor(0, row);
            Console.Write($"  [{label,-8}] ");
            for (int i = 0; i < total; i++)
                Console.Write(i < filled ? "█" : "░");
            int pct = filled * 100 / total;
            Console.Write($" {pct,3}%");
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  08 · CANCELLABLE CHAIN  ·  Break.AndStop + .Continue()     ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  An operation chain: LOAD → VALIDATE → PROCESS.             ║");
            Console.WriteLine("  ║  Validation fails and the failure is forwarded up so that   ║");
            Console.WriteLine("  ║  PROCESS is skipped AND Parent is cancelled.                ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }
    }
}