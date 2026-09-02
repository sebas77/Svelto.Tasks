using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;

namespace OrderedLoading
{
    static class Program
    {
        const int PipelineRow = 9;
        const int DownloadProgressRow = 15;
        const int ParseProgressRow = 16;
        const int InitializeProgressRow = 17;
        const int SummaryRow = 20;

        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 09 Ordered Loading"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            var log = new List<string>();

            IEnumerator<TaskContract> DownloadStage()
            {
                log.Add("download-start");
                for (int i = 0; i <= 10; i++)
                {
                    Bar(DownloadProgressRow, "DOWNLOAD", i, 10, "▓");
                    Thread.Sleep(50);
                }
                yield return TaskContract.Yield.It;
                log.Add("download-done");
                StageDone(DownloadProgressRow, "DOWNLOAD");
            }

            IEnumerator<TaskContract> ParseStage()
            {
                log.Add("parse-start");
                for (int i = 0; i <= 10; i++)
                {
                    Bar(ParseProgressRow, "PARSE   ", i, 10, "▒");
                    Thread.Sleep(45);
                }
                yield return TaskContract.Yield.It;
                log.Add("parse-done");
                StageDone(ParseProgressRow, "PARSE   ");
            }

            IEnumerator<TaskContract> InitStage()
            {
                log.Add("init-start");
                for (int i = 0; i <= 10; i++)
                {
                    Bar(InitializeProgressRow, "INIT    ", i, 10, "█");
                    Thread.Sleep(40);
                }
                yield return TaskContract.Yield.It;
                log.Add("init-done");
                StageDone(InitializeProgressRow, "INIT    ");
            }

            var serial = new SerialTaskCollection("LevelLoader");
            serial.Add(DownloadStage());
            serial.Add(ParseStage());
            serial.Add(InitStage());

            DrawPipeline();

            serial.Complete(10000);

            SafeSetCursor(0, SummaryRow);
            Console.WriteLine("  ╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  ✅ Level loaded — stages ran STRICTLY in order         ║");
            Console.WriteLine("  ╠════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  Execution log:                                         ║");
            foreach (var entry in log)
                Console.WriteLine($"  ║    • {entry,-48}║");
            Console.WriteLine("  ╚════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  💡 SerialTaskCollection: task B does NOT start until task A finishes.");
            Console.WriteLine();
        }

        static void DrawPipeline()
        {
            SafeSetCursor(0, PipelineRow);
            Console.WriteLine("   ┌──────────┐     ┌──────────┐     ┌──────────┐");
            Console.WriteLine("   │ DOWNLOAD │ ──▶ │  PARSE   │ ──▶ │   INIT   │");
            Console.WriteLine("   └──────────┘     └──────────┘     └──────────┘");
            Console.WriteLine();
        }

        static void Bar(int row, string label, int filled, int total, string fill)
        {
            SafeSetCursor(0, row);
            Console.Write($"  [{label}] ");
            for (int i = 0; i < total; i++)
                Console.Write(i < filled ? fill : "░");
            int pct = filled * 100 / total;
            Console.Write($" {pct,3}%");
        }

        static void StageDone(int row, string label)
        {
            SafeSetCursor(0, row);
            Console.Write($"  [{label}] ");
            for (int i = 0; i < 10; i++)
                Console.Write("█");
            Console.Write(" 100% ✅");
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  09 · ORDERED LOADING  ·  SerialTaskCollection              ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  Game level loading: Download → Parse → Initialize.         ║");
            Console.WriteLine("  ║  Stages run STRICTLY sequentially — no overlap.              ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }
    }
}
