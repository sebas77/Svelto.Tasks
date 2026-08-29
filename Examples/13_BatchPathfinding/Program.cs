using System;
using System.Threading;
using Svelto.Tasks.Parallelism;

#pragma warning disable CS0436

namespace Example13_BatchPathfinding
{
    struct PathfindingJob : ISveltoJob
    {
        public int[] results;
        public int[] threadAssign;

        public void Update(int index)
        {
            results[index] = index;
            threadAssign[index] = Thread.CurrentThread.ManagedThreadId;
        }

        public void Dispose() { }
    }

    static class Program
    {
        const int TotalUnits = 1000;
        const int ThreadCount = 4;
        static readonly char[] _spin = { '|', '/', '-', '\\' };

        static void SafeClear() { try { Console.Clear(); } catch { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch { } }
        static void SafeTitle(string title) { try { Console.Title = title; } catch { } }
        static void SafeReadKey() { try { Console.ReadKey(); } catch { } }

        static void Main()
        {
            SafeTitle("Svelto.Tasks — Parallel Job Collection");
            SafeCursorVisible(false);

            PrintBanner();

            var results = new int[TotalUnits];
            var threadAssign = new int[TotalUnits];

            for (int i = 0; i < TotalUnits; i++)
                results[i] = -1;

            var job = new PathfindingJob
            {
                results = results,
                threadAssign = threadAssign
            };

            using var collection = new MultiThreadedParallelJobCollection<PathfindingJob>(
                "PathfindingBatch", ThreadCount, false);

            Console.WriteLine();
            Console.WriteLine("  🔧 Submitting {0} units across {1} threads...", TotalUnits, ThreadCount);

            collection.Add(job, TotalUnits);
            collection.Complete();

            int done = 0;
            for (int i = 0; i < TotalUnits; i++)
                if (results[i] == i) done++;

            var distinctThreads = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < TotalUnits; i++)
                distinctThreads.Add(threadAssign[i]);

            ShowGrid(results, threadAssign, distinctThreads);

            Console.WriteLine();
            Console.WriteLine("  ──────────────────────────────────────────────────────");
            Console.WriteLine("  📊 Results:");
            Console.WriteLine("     Units pathfound : {0}/{1}  {2}", done, TotalUnits,
                done == TotalUnits ? "✅ ALL DONE" : "❌ INCOMPLETE");
            Console.WriteLine("     Threads used     : {0}", distinctThreads.Count);
            foreach (int tid in distinctThreads)
            {
                int count = 0;
                for (int i = 0; i < TotalUnits; i++)
                    if (threadAssign[i] == tid) count++;
                Console.WriteLine("       └─ Thread {0,2}: processed {1,4} units", tid, count);
            }
            Console.WriteLine("  ──────────────────────────────────────────────────────");

            Console.WriteLine();
            Console.WriteLine("  ✅ Done. Press any key to exit.");
            SafeCursorVisible(true);
            SafeReadKey();
        }

        static void ShowGrid(int[] results, int[] threadAssign, System.Collections.Generic.HashSet<int> threads)
        {
            int[] threadIds = new int[threads.Count];
            threads.CopyTo(threadIds);
            Array.Sort(threadIds);

            //map every unit to its cell: a cell shows the thread that processed it, or '?' if
            //the units of the cell were split across threads (not the case with contiguous slices)
            int[,] threadMap = new int[10, 10];
            for (int row = 0; row < 10; row++)
                for (int col = 0; col < 10; col++)
                    threadMap[row, col] = -1;

            for (int i = 0; i < TotalUnits; i++)
            {
                int row = i / 100;
                int col = (i / 10) % 10;
                int tIndex = Array.IndexOf(threadIds, threadAssign[i]);

                if (threadMap[row, col] == -1 || threadMap[row, col] == tIndex)
                    threadMap[row, col] = tIndex;
                else
                    threadMap[row, col] = -2; //mixed
            }

            char[] cellChars = { '█', '▓', '▒', '░' };
            ConsoleColor[] cellColors =
            {
                ConsoleColor.Red, ConsoleColor.Green,
                ConsoleColor.Cyan, ConsoleColor.Yellow
            };

            Console.WriteLine("  🗺️  Pathfinding Grid (10×10, each cell = 10 units):");
            Console.WriteLine("  ┌──────────────────────────────────────────┐");

            for (int row = 0; row < 10; row++)
            {
                Console.Write("  │ ");
                for (int col = 0; col < 10; col++)
                {
                    int tIdx = threadMap[row, col];
                    if (tIdx == -2) //cell split across threads
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("??");
                    }
                    else
                    {
                        Console.ForegroundColor = cellColors[tIdx % cellColors.Length];
                        Console.Write(cellChars[tIdx % cellChars.Length]);
                        Console.Write(cellChars[tIdx % cellChars.Length]);
                    }
                }
                Console.ResetColor();
                Console.WriteLine(" │");
            }

            Console.WriteLine("  └──────────────────────────────────────────┘");

            Console.WriteLine();
            for (int t = 0; t < threadIds.Length; t++)
            {
                int count = 0;
                for (int i = 0; i < TotalUnits; i++)
                    if (threadAssign[i] == threadIds[t]) count++;

                Console.ForegroundColor = cellColors[t % cellColors.Length];
                Console.Write("  {0}", cellChars[t % cellChars.Length]);
                Console.ResetColor();
                Console.Write(" Thread {0,2}: [", threadIds[t]);

                const int barWidth = 30;
                int barLen = (int)Math.Min(barWidth, (long)count * barWidth / TotalUnits);
                Console.ForegroundColor = cellColors[t % cellColors.Length];
                Console.Write(new string('█', barLen));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(new string('░', barWidth - barLen));
                Console.ResetColor();
                Console.WriteLine("] {0,4}/{1} done  ✓", count, TotalUnits);
            }
        }

        static void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║   🗺️  Svelto.Tasks Example 13 — Batch Pathfinding          ║");
            Console.WriteLine("  ║   {0} units, {1} threads, ISveltoJob + ParallelJobCollection  ║",
                TotalUnits, ThreadCount);
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }
    }
}