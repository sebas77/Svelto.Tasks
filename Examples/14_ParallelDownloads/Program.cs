using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Svelto.Tasks.Parallelism.ExtraLean;

#pragma warning disable CS0436

namespace Example14_ParallelDownloads
{
    class DownloadProgress
    {
        public volatile int Percent;
        public volatile bool Done;
        public volatile int ThreadId;
    }

    struct DownloadTask : IParallelTask
    {
        readonly int _stepDelayMs;
        readonly int _totalSteps;
        readonly DownloadProgress _progress;
        int _stepsLeft;

        public DownloadTask( int steps, int stepDelayMs, DownloadProgress progress)
        {
            _stepDelayMs = stepDelayMs;
            _totalSteps = steps;
            _progress = progress;
            _stepsLeft = steps;
        }

        public object Current => null;

        public bool MoveNext()
        {
            //the collection hands queued tasks to whichever runner is idle: each MoveNext
            //records the thread it runs on, so the host can see the self-balancing live
            _progress.ThreadId = Thread.CurrentThread.ManagedThreadId;

            if (_stepsLeft == 0)
            {
                _progress.Percent = 100;
                _progress.Done = true;
                return false;
            }

            Thread.Sleep(_stepDelayMs);
            _stepsLeft--;

            _progress.Percent = 100 - _stepsLeft * 100 / _totalSteps;

            if (_stepsLeft == 0)
            {
                _progress.Percent = 100;
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
        const int TotalDownloads = 400;
        const int ThreadCount = 4;
        const int MonitorFrameMs = 100;

        static readonly char[] _spin = { '|', '/', '-', '\\' };
        static DownloadProgress[] _progresses;
        static string[] _names;
        static int[] _sequentialMs; //per-download cost, used for the sequential-estimate line
        static volatile bool _monitoring = true;
        static long _totalSequentialMs;

        static void SafeClear() { try { Console.Clear(); } catch { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch { } }
        static void SafeTitle(string title) { try { Console.Title = title; } catch { } }

        static void Main()
        {
            SafeTitle("Svelto.Tasks — 400 Parallel Downloads on 4 Threads");
            SafeCursorVisible(false);

            PrintBanner();

            //uneven download sizes (seeded for reproducibility): with far more tasks than
            //threads, the collection's idle-runner stealing is what keeps all threads busy
            var random = new Random(7);
            _progresses = new DownloadProgress[TotalDownloads];
            _names = new string[TotalDownloads];
            _sequentialMs = new int[TotalDownloads];

            using var collection = new MultiThreadedParallelTaskCollection<DownloadTask>("Downloads", ThreadCount, false);

            //the wave-end signal: fires once, on the thread calling Complete()
            bool onCompleteFired = false;
            collection.onComplete += () => onCompleteFired = true;

            for (int i = 0; i < TotalDownloads; i++)
            {
                int steps = 2 + random.Next(7);        //2..8 steps
                int delayMs = 8 + random.Next(7);      //8..14 ms per step
                _names[i] = $"File_{i:D3}.zip";
                _sequentialMs[i] = steps * delayMs;
                _totalSequentialMs += _sequentialMs[i];
                _progresses[i] = new DownloadProgress();
                collection.Add(new DownloadTask( steps, delayMs, _progresses[i]));
            }

            var monitorThread = new Thread(MonitorProgress)
            {
                IsBackground = true,
                Name = "ProgressMonitor"
            };
            monitorThread.Start();

            var clock = Stopwatch.StartNew();
            collection.Complete(); //one wave: all 400 downloads distributed over 4 runners
            clock.Stop();

            _monitoring = false;
            monitorThread.Join();

            DrawFinal(clock.ElapsedMilliseconds, onCompleteFired);

            Console.WriteLine();
            Console.WriteLine("  ✅ All downloads complete!");
            SafeCursorVisible(true);
        }

        static void MonitorProgress()
        {
            int frame = 0;
            while (_monitoring)
            {
                DrawProgress(frame++);
                Thread.Sleep(MonitorFrameMs);
            }
        }

        static void DrawProgress(int frame)
        {
            SafeSetCursor(0, 9);
            string spinner = _spin[frame % 4].ToString();

            int done = 0;
            long percentSum = 0;
            var perThreadInFlight = new Dictionary<int, int>();
            var perThreadDone = new Dictionary<int, int>();

            for (int i = 0; i < TotalDownloads; i++)
            {
                var p = _progresses[i];
                percentSum += p.Percent;

                if (p.Done)
                {
                    done++;
                    Increment(perThreadDone, p.ThreadId);
                }
                else if (p.ThreadId > 0)
                {
                    Increment(perThreadInFlight, p.ThreadId);
                }
            }

            int overall = (int)(percentSum / TotalDownloads);
            int barLen = 40;
            int filled = overall * barLen / 100;

            Console.WriteLine("  ┌────────────────────────────────────────────────────────────┐  ");
            Console.WriteLine("  │  📦 400 downloads / 4 threads  {0}   done {1,3}/400          │  ",
                spinner, done);
            Console.WriteLine("  ├────────────────────────────────────────────────────────────┤  ");
            Console.WriteLine("  │  Overall [{0}{1}] {2,3}%                       │  ",
                new string('█', filled), new string('░', barLen - filled), overall);

            foreach (int tid in perThreadInFlight.Keys)
            {
                int completed = perThreadDone.TryGetValue(tid, out int c) ? c : 0;
                Console.WriteLine("  │   T{0:D2}  in flight {1,3}   completed {2,3}                     │  ",
                    tid % 100, perThreadInFlight[tid], completed);
            }

            for (int i = perThreadInFlight.Count; i < ThreadCount; i++)
                Console.WriteLine("  │                                                            │  ");

            Console.WriteLine("  ├────────────────────────────────────────────────────────────┤  ");
            Console.WriteLine("  │  idle threads steal the next queued download as they        │  ");
            Console.WriteLine("  │  finish: per-thread totals stay balanced despite uneven     │  ");
            Console.WriteLine("  │  file sizes. Watch the completed counts converge.           │  ");
            Console.WriteLine("  └────────────────────────────────────────────────────────────┘  ");
        }

        static void Increment(Dictionary<int, int> dictionary, int key)
        {
            dictionary[key] = dictionary.TryGetValue(key, out int current) ? current + 1 : 1;
        }

        static void DrawFinal(long elapsedMs, bool onCompleteFired)
        {
            var perThread = new Dictionary<int, int>();
            for (int i = 0; i < TotalDownloads; i++)
                Increment(perThread, _progresses[i].ThreadId);

            var threadIds = new List<int>(perThread.Keys);
            threadIds.Sort();

            SafeSetCursor(0, 9);
            Console.WriteLine("  ┌────────────────────────────────────────────────────────────┐  ");
            Console.WriteLine("  │  📦 Parallel Downloads  ✅ ALL {0} COMPLETE                    │  ",
                TotalDownloads);
            Console.WriteLine("  ├────────────────────────────────────────────────────────────┤  ");
            Console.WriteLine("  │  📊 Results                                                 │  ");

            foreach (int tid in threadIds)
                Console.WriteLine("  │    T{0:D2} processed {1,3} downloads                        │  ",
                    tid % 100, perThread[tid]);

            Console.WriteLine("  ├────────────────────────────────────────────────────────────┤  ");
            Console.WriteLine("  │  elapsed (4 threads)      : {0,6} ms                        │  ", elapsedMs);
            Console.WriteLine("  │  sequential estimate      : {0,6} ms                        │  ", _totalSequentialMs);
            Console.WriteLine("  │  speedup                  : {0,6} x                         │  ",
                (double)_totalSequentialMs / Math.Max(1, elapsedMs));
            Console.WriteLine("  │  onComplete fired         : {0,-31}│  ",
                onCompleteFired ? "yes" : "NO — unexpected!");
            Console.WriteLine("  │  per-thread counts ~equal despite uneven sizes:            │  ");
            Console.WriteLine("  │  that even split is the self-balancing this collection     │  ");
            Console.WriteLine("  │  exists for. With tasks <= threads, a runner pool is       │  ");
            Console.WriteLine("  │  the leaner choice (see Example 22).                       │  ");
            Console.WriteLine("  └────────────────────────────────────────────────────────────┘  ");
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║   📦 Svelto.Tasks Example 14 — Parallel Downloads           ║");
            Console.WriteLine("  ║   {0} files × {1} threads, self-balancing steal queue         ║",
                TotalDownloads, ThreadCount);
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  Far more tasks than threads: runners that finish a download");
            Console.WriteLine("  immediately claim the next queued one (work stealing), so the");
            Console.WriteLine("  wave finishes in roughly total-work / threads time.");
            Console.WriteLine();
        }
    }
}
