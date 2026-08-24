using System;
using System.Threading;
using Svelto.Tasks.Parallelism;

#pragma warning disable CS0436

namespace Example14_ParallelDownloads
{
    class DownloadProgress
    {
        public volatile int Percent;
        public volatile bool Done;
        public volatile int ThreadId;
    }

    class DownloadTask : IParallelTask
    {
        readonly string _fileName;
        readonly int _stepDelayMs;
        readonly int _increment;
        readonly DownloadProgress _progress;

        public DownloadTask(string fileName, int totalSteps, int stepDelayMs, DownloadProgress progress)
        {
            _fileName = fileName;
            _stepDelayMs = stepDelayMs;
            _progress = progress;
            //rounded so the declared step count actually reaches 100% (integer division
            //would silently stretch the download beyond its declared duration)
            _increment = Math.Max(1, (int)Math.Round(100f / totalSteps));
        }

        public object Current => null;

        public bool MoveNext()
        {
            _progress.ThreadId = Thread.CurrentThread.ManagedThreadId;

            if (_progress.Percent >= 100)
            {
                _progress.Done = true;
                return false;
            }

            Thread.Sleep(_stepDelayMs);

            _progress.Percent = Math.Min(100, _progress.Percent + _increment);

            if (_progress.Percent >= 100)
            {
                _progress.Done = true;
                return false;
            }

            return true;
        }

        public void Reset() { }

        public void Dispose() { }
    }

    static class Program
    {
        static readonly char[] _spin = { '|', '/', '-', '\\' };
        static DownloadProgress[] _progresses;
        static (string name, int steps, int delay)[] _specs;
        static bool _monitoring = true;

        static void SafeClear() { try { Console.Clear(); } catch { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch { } }
        static void SafeTitle(string title) { try { Console.Title = title; } catch { } }
        static void SafeReadKey() { try { Console.ReadKey(); } catch { } }

        static void Main()
        {
            SafeTitle("Svelto.Tasks — Parallel Downloads");
            SafeCursorVisible(false);

            PrintBanner();

            _specs = new (string name, int steps, int delay)[]
            {
                ("File_1.zip", 20, 40),
                ("File_2.zip", 10, 80),
                ("File_3.zip", 25, 30),
                ("File_4.zip", 15, 60),
            };

            _progresses = new DownloadProgress[4];
            for (int i = 0; i < 4; i++)
                _progresses[i] = new DownloadProgress();

            using var collection = new Svelto.Tasks.Parallelism.ExtraLean
                .MultiThreadedParallelTaskCollection("Downloads", 4, false);

            for (int i = 0; i < 4; i++)
                collection.Add(new DownloadTask(_specs[i].name, _specs[i].steps, _specs[i].delay, _progresses[i]));

            var monitorThread = new Thread(MonitorProgress)
            {
                IsBackground = true,
                Name = "ProgressMonitor"
            };
            monitorThread.Start();

            collection.Complete();

            _monitoring = false;
            monitorThread.Join();

            DrawFinal();

            Console.WriteLine();
            Console.WriteLine("  ✅ All downloads complete! Press any key to exit.");
            SafeCursorVisible(true);
            SafeReadKey();
        }

        static void MonitorProgress()
        {
            int frame = 0;
            while (_monitoring)
            {
                DrawProgress(frame++);
                Thread.Sleep(80);
            }
        }

        static void DrawProgress(int frame)
        {
            SafeSetCursor(0, 9);
            string spinner = _spin[frame % 4].ToString();

            Console.WriteLine("  ┌──────────────────────────────────────────────────────────┐  ");
            Console.WriteLine("  │  📦 Parallel Downloads  {0}  4 threads, 4 files simultan. │  ", spinner);
            Console.WriteLine("  ├──────────────────────────────────────────────────────────┤  ");

            for (int i = 0; i < 4; i++)
            {
                var p = _progresses[i];
                int pct = p.Percent;
                int barLen = 20;
                int filled = pct * barLen / 100;
                string bar = new string('█', filled) + new string('░', barLen - filled);

                string status;
                if (p.Done)
                    status = "✓ done";
                else if (pct > 0)
                    status = "⬇ down";
                else
                    status = "⏳ ...";

                Console.Write("  │  {0,-12} [{1}] {2,3}% {3}",
                    _specs[i].name, bar, pct, status);
                Console.WriteLine("  T{0:D2}    │  ",
                    p.ThreadId > 0 ? p.ThreadId % 100 : 0);
            }

            Console.WriteLine("  ├──────────────────────────────────────────────────────────┤  ");

            int totalPct = 0;
            for (int i = 0; i < 4; i++)
                totalPct += _progresses[i].Percent;
            int avg = totalPct / 4;

            int overallFilled = avg * 20 / 100;
            string overallBar = new string('█', overallFilled) + new string('░', 20 - overallFilled);

            Console.WriteLine("  │  📊 Overall:  [{0}] {1,3}%  {2,2}/4 complete          │  ",
                overallBar, avg, CountDone());
            Console.WriteLine("  └──────────────────────────────────────────────────────────┘  ");
        }

        static int CountDone()
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
                if (_progresses[i].Done) count++;
            return count;
        }

        static void DrawFinal()
        {
            SafeSetCursor(0, 9);
            Console.WriteLine("  ┌──────────────────────────────────────────────────────────┐  ");
            Console.WriteLine("  │  📦 Parallel Downloads  ✅ COMPLETE                       │  ");
            Console.WriteLine("  ├──────────────────────────────────────────────────────────┤  ");

            for (int i = 0; i < 4; i++)
            {
                Console.Write("  │  {0,-12} [{1}] 100% ✓ done          T{2:D2}    │  ",
                    _specs[i].name, new string('█', 20), _progresses[i].ThreadId % 100);
                Console.WriteLine();
            }

            Console.WriteLine("  ├──────────────────────────────────────────────────────────┤  ");
            Console.WriteLine("  │  📊 Overall:  [████████████████████] 100%  4/4 complete          │  ");
            Console.WriteLine("  └──────────────────────────────────────────────────────────┘  ");
        }

        static void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║   📦 Svelto.Tasks Example 14 — Parallel Downloads          ║");
            Console.WriteLine("  ║   4 files × 4 threads, IParallelTask + ParallelTaskCollection║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  ⬇ = downloading   ✓ = complete   T## = thread id");
            Console.WriteLine();
        }
    }
}