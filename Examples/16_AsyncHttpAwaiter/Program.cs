using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Svelto.Tasks.Lean;

#pragma warning disable

class Program
{
    static SteppableRunner _runner;

    static async Task<string> SimulateHttpRequest()
    {
        await Task.Delay(800).RunOn(_runner);

        return "{ \"players\": [\"Alice\", \"Bob\", \"Charlie\"] }";
    }

    static void Main()
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch {}
        try { Console.CursorVisible = false; } catch {}

        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                    ASYNC HTTP AWAITER                      │");
        Console.WriteLine("│             Svelto.Tasks Awaiter Interop                   │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Scenario: Simulate an async HTTP request using the Svelto");
        Console.WriteLine("  awaiter. The SteppableRunner ticks while awaiting Task.Delay.");
        Console.WriteLine();

        _runner = new SteppableRunner("HttpRunner");

        Console.WriteLine("  ┌─────────┐                  ┌─────────┐");
        Console.WriteLine("  │ CLIENT  │                  │ SERVER  │");
        Console.WriteLine("  └────┬────┘                  └────┬────┘");
        Console.WriteLine("       │                            │");
        Console.WriteLine("       │  >>>  HTTP GET /api  >>>   │");
        Console.WriteLine("       │                            │");
        Console.WriteLine("       v                            v");
        Console.WriteLine();
        Console.Write("  Sending request... ");

        var task = SimulateHttpRequest();
        Thread.Sleep(10);

        var spinners = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        var sw = Stopwatch.StartNew();
        int frame = 0;
        const int barWidth = 36;

        while (!task.IsCompleted)
        {
            _runner.Step(); //if I don't step the runner, the task won't continue and the awaiter will never complete, so the loop will never exit
            frame++;

            double progress = Math.Min(1.0, sw.Elapsed.TotalMilliseconds / 800.0);
            int filled = (int)(progress * barWidth);
            string bar = new string('█', filled) + new string('░', barWidth - filled);
            string spinner = spinners[frame % spinners.Length];

            Console.Write("\r  {0} Runner ticking  [{1}] {2,3:0}%  step #{3,3}  ", spinner, bar, progress * 100, frame);

            Thread.Sleep(20);
        }

        sw.Stop();

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("  ┌─────────┐                  ┌─────────┐");
        Console.WriteLine("  │ CLIENT  │                  │ SERVER  │");
        Console.WriteLine("  └─────────┘                  └─────────┘");
        Console.WriteLine();
        Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  ✅  HTTP RESPONSE RECEIVED!                               ║");
        Console.WriteLine("  ║  Status: 200 OK                                           ║");
        Console.WriteLine("  ║  Body: " + task.Result.PadRight(55) + "║");
        Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("  Runner stepped {0} times in {1:0}ms", frame, sw.Elapsed.TotalMilliseconds);
        Console.WriteLine();
        Console.WriteLine("  ┌─ How it worked ────────────────────────────────────────────┐");
        Console.WriteLine("  │ 1. SimulateHttpRequest() runs until the first await        │");
        Console.WriteLine("  │ 2. Task.Delay(800).RunOn(runner) creates a TaskRunnerAwaiter│");
        Console.WriteLine("  │ 3. The awaiter registers the continuation on the .NET Task  │");
        Console.WriteLine("  │ 4. Each Step() ticks freely while the delay runs elsewhere  │");
        Console.WriteLine("  │ 5. On completion the Task posts the continuation back here  │");
        Console.WriteLine("  │ 6. The next Step() resumes the async method → task completes│");
        Console.WriteLine("  └────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Key: .RunOn(runner) on a Task returns a TaskRunnerAwaiter that");
        Console.WriteLine("  bridges async/await into Svelto.Tasks — continuations run on");
        Console.WriteLine("  the Svelto runner, NOT the default sync context.");
        Console.WriteLine();

        try { Console.CursorVisible = true; } catch {}
        _runner.Dispose();
    }
}