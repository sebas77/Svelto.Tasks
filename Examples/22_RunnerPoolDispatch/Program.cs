using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.ExtraLean;

#pragma warning disable CS0436

namespace Example22_RunnerPoolDispatch
{
    /// <summary>
    /// An ExtraLean root task: yields only null (one step per MoveNext = true), records
    /// its own progress, and counts its completion through an Interlocked counter so the
    /// host can track "all requests done" without the pool providing any batch API.
    /// </summary>
    class RequestTask : IEnumerator
    {
        internal RequestTask(int id, int steps, int stepDelayMs)
        {
            Id = id;
            _steps = steps;
            _stepDelayMs = stepDelayMs;
        }

        internal int Id { get; }

        internal volatile int Percent;
        internal volatile bool Done;
        internal volatile int RunnerIndex = -1; //set by the host from the dispatch round-robin

        public bool MoveNext()
        {
            if (_stepsLeft == 0)
            {
                Percent = 100;
                Done = true;
                Interlocked.Increment(ref Program.CompletedRequests);
                return false;
            }

            Thread.Sleep(_stepDelayMs);
            _stepsLeft--;
            Percent = 100 - _stepsLeft * 100 / _steps;

            return true;
        }

        int _stepsLeft;
        readonly int _steps;
        readonly int _stepDelayMs;

        public object Current => null;
        public void Reset() { }
        public void Dispose() { }
    }

    static class Program
    {
        internal static int CompletedRequests;

        const int ThreadCount = 4;
        const int RequestCount = 16; //4 requests per runner, all in flight simultaneously

        static readonly char[] _spin = { '|', '/', '-', '\\' };
        static RequestTask[] _requests;

        static void SafeClear() { try { Console.Clear(); } catch { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch { } }
        static void SafeTitle(string title) { try { Console.Title = title; } catch { } }
        static void SafeReadKey() { try { Console.ReadKey(); } catch { } }

        static void Main()
        {
            SafeTitle("Svelto.Tasks — MultiThreadRunnerPool Fan-Out");
            SafeCursorVisible(false);

            PrintBanner();

            using var pool = new MultiThreadRunnerPool("request-pool", ThreadCount, RequestCount);

            //Dispatch is a strict round-robin at AddTask time: request i lands on
            //runner i % 4. Unlike MultiThreadedParallelTaskCollection there is no
            //shared queue, no wrapper, no completion counter — and no rebalancing.
            _requests = new RequestTask[RequestCount];
            var random = new Random(11);

            for (int i = 0; i < RequestCount; i++)
            {
                var request = new RequestTask(i, 5 + random.Next(4), 20 + random.Next(20));
                request.RunnerIndex = i % pool.numberOfRunners;
                _requests[i] = request;
                request.RunOn(pool);
            }

            DrawLive();

            Console.WriteLine();
            Console.WriteLine("  ┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │  📊 Dispatch table (round-robin, deterministic)             │");
            Console.WriteLine("  ├────────────────────────────────────────────────────────────┤");

            for (int runner = 0; runner < pool.numberOfRunners; runner++)
            {
                var handled = new List<int>();
                for (int i = 0; i < RequestCount; i++)
                    if (_requests[i].RunnerIndex == runner)
                        handled.Add(i);

                Console.WriteLine("  │  runner #{0} → requests [{1}]                       │",
                    runner, string.Join(", ", handled));
            }

            Console.WriteLine("  ├────────────────────────────────────────────────────────────┤");
            Console.WriteLine("  │  ✅ all {0} requests completed (host-counted, not pool-counted){1}│",
                RequestCount, CompletedRequests == RequestCount ? " " : "!");
            Console.WriteLine("  │                                                             │");
            Console.WriteLine("  │  💡 This is the tasks <= threads sweet spot: each AddTask    │");
            Console.WriteLine("  │     is one atomic increment + a direct hand-off. No feed     │");
            Console.WriteLine("  │     queue, no wrapper, no wave counter. The cost: dispatch   │");
            Console.WriteLine("  │     never rebalances — a slow request makes ITS runner lag   │");
            Console.WriteLine("  │     while others go idle. For more tasks than threads, use   │");
            Console.WriteLine("  │     MultiThreadedParallelTaskCollection (Example 14).        │");
            Console.WriteLine("  └────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("  ✅ Done. Press any key to exit.");
            SafeCursorVisible(true);
            SafeReadKey();
        }

        static void DrawLive()
        {
            //All requests are in flight at once: each runner interleaves its four
            //assignments, one MoveNext per pass. The host simply polls its own objects.
            var spinner = new SpinWait();
            int frame = 0;

            while (CompletedRequests < RequestCount)
            {
                SafeSetCursor(0, 9);
                Console.WriteLine("  ┌────────────────────────────────────────────────────────────┐  ");
                Console.WriteLine("  │  🖧 requests in flight  {0}   completed {1,2}/{2}              │  ",
                    _spin[frame++ % 4], CompletedRequests, RequestCount);
                Console.WriteLine("  ├────────────────────────────────────────────────────────────┤  ");

                for (int runner = 0; runner < ThreadCount; runner++)
                {
                    Console.Write("  │  #{0} ", runner);

                    for (int slot = 0; slot < RequestCount / ThreadCount; slot++)
                    {
                        var request = _requests[runner + slot * ThreadCount];
                        Console.Write("R{0:D2}:{1,3}% {2} ",
                            request.Id,
                            request.Percent,
                            request.Done ? "✓" : "▒ ");
                    }

                    Console.WriteLine("         │  ");
                }

                Console.WriteLine("  └────────────────────────────────────────────────────────────┘  ");

                if (spinner.NextSpinWillYield)
                    Thread.Sleep(30);
                spinner.SpinOnce();
            }

            Thread.Sleep(50); //let the last console flush land before the summary
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║   🖧 Svelto.Tasks Example 22 — Runner Pool Dispatch          ║");
            Console.WriteLine("  ║   {0} requests round-robin to {1} runners, host-counted        ║",
                RequestCount, ThreadCount);
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  Lean fan-out for tasks <= threads: one atomic dispatch per");
            Console.WriteLine("  request, no shared queue, no wave counter, no rebalancing.");
            Console.WriteLine();
        }
    }
}
