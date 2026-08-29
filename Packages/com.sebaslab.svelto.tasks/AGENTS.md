# AGENTS.md — Instructions for AI coding agents

Single source of truth for AI agents working with **Svelto.Tasks**. This file lives inside the package so it travels with the code: if you copy just the `Packages/` folder into your own project, your agents can still understand the library from this file. Tool-specific files (`CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`) only point here — do not duplicate content.

## What this is

**Svelto.Tasks** — a platform-agnostic C# library for running serial and parallel asynchronous tasks ("coroutines"). Tasks are **iterators (`IEnumerator`), not `async`/`await` Tasks**: a runner calls `MoveNext()` on them according to a flow-modifier strategy. This gives precise control over execution flow, scheduling, frame budgets, and threading.

The package layout:

```
com.sebaslab.svelto.tasks/
    Svelto.Tasks/                            # the library (netstandard2.1)
        Collections/                         #   SerialTaskCollection, ParallelTaskCollection
        Enumerators/                         #   WaitForSecondsEnumerator, WaitForSignal<T>, ...
        FlowModifiers/                       #   StandardFlow, SerialFlow, StaggeredFlow, TimeBoundFlow, TimeSlicedFlow
        Parallelism/                         #   MultiThreadedParallelTaskCollection, MultiThreadedParallelJobCollection<T>
        Runners/                             #   SteppableRunner, MultiThreadRunner, SyncRunner (Lean + ExtraLean variants)
        Tasks/                               #   TaskContract, Lean/ExtraLean task wrappers
    Svelto.Tasks.Tests~/                     # NUnit tests
    .aiguides/AI_GUIDE_Svelto.Tasks.md       # DEEP API reference — read before non-trivial changes
```

Svelto.Tasks depends on the companion package `com.sebaslab.svelto.common` (FasterList, pooling, logging). Its deep reference lives at `com.sebaslab.svelto.common/.aiguides/AI_GUIDE_Svelto.Common.md`.

## When to use Svelto.Tasks

Use it when you need any of the following; otherwise prefer plain `async`/`await` or `System.Threading.Tasks`.

| Need | Use |
|---|---|
| Tick cooperative coroutines from your own host loop | Lean task + `SteppableRunner` |
| Minimal-overhead coroutine that only waits | ExtraLean task |
| Run work on a dedicated background thread | `MultiThreadRunner` |
| Run one task synchronously to completion | `.Complete()` |
| Data-parallel work split across N threads | `MultiThreadedParallelJobCollection<TJob>` |
| Many independent tasks across N threads | `MultiThreadedParallelTaskCollection` |
| Sequential pipeline (A then B then C) | `SerialTaskCollection` |
| Several cooperative tasks progressing together per tick | `ParallelTaskCollection` |
| Limit tasks per tick or bound ms per tick | `StaggeredFlow(n)` / `TimeBoundFlow(ms)` / `TimeSlicedFlow(ms)` |
| Reusable task without a new iterator allocation after pool warm-up | pooled iterator block (`while(true) { yield return Break.It; }`) |
| Cross-thread handshake | subclass `WaitForSignal<T>` |
| Interop with existing async code | `await someDotNetTask.RunOn(runner)` (Task/ValueTask into a runner), `await enumerator.ToTask<T>(runner)` (iterator out to .NET, reference results) |

Do NOT use it for: ordinary I/O-bound async code with no scheduling requirements, code that must return values through standard `Task<T>` pipelines (use `.ToTask<T>()` only at interop boundaries).

## How to use Svelto.Tasks (minimum viable knowledge)

### 1. Write a task as an iterator

```csharp
using Svelto.Tasks;
using static Svelto.Tasks.TaskContract; // enables `Yield.It`, `Break.It`, ...

IEnumerator<TaskContract> LoadAndProcess()          // Lean task
{
    //enumerators cannot be yielded directly (no implicit conversion to TaskContract):
    //wait through a continuation, or poll with MoveNext + Yield.It
    yield return new WaitForSecondsEnumerator(2f).Continue();   // wait 2 seconds

    for (int i = 0; i < 10; i++)
    {
        DoWork(i);
        yield return Yield.It;                      // REQUIRED inside loops — see gotchas
    }

    yield return 42;                                // int stored inline in TaskContract; read with ToInt()
}
```

### 2. Choose a runner

```csharp
var runner = new SteppableRunner("GameLoopRunner");  // you call Step() every frame/tick
// or
var runner = new MultiThreadRunner("BgWorker");      // dedicated background thread
```

### 3. Start and compose tasks

```csharp
// run on this runner, don't wait:
task.RunOn(runner);                                  // returns Continuation (check .isRunning if needed)

// parent waits for child on the SAME runner:
yield return Child().Continue();

// fire-and-forget on same runner (parent does NOT wait):
yield return SideWork().Forget();

// wait for a task on ANOTHER runner:
var cont = OtherTask().RunOn(otherRunner);
yield return cont;

// block until done (thread-local SyncRunner):
enumerator.Complete();                                // optional timeout: Complete(timeoutMs)

// await from async code (Lean only, reference results):
string result = await enumerator.ToTask<string>(runner);
```

**`.Continue()` vs `.RunOn(runner)` vs `.Forget()`:**

| Method | Parent waits? | Runs on |
|---|---|---|
| `.Continue()` | Yes | Same runner as parent |
| `.RunOn(runner)` | Only if you `yield return` the returned `Continuation` | The given runner |
| `.Forget()` | **No** (parent continues immediately — child body may not have started) | Same runner (scheduled) |

### 4. Group tasks

```csharp
var serial = new SerialTaskCollection();
serial.Add(TaskA()); serial.Add(TaskB());            // A → B sequentially
serial.RunOn(runner);

var parallel = new ParallelTaskCollection();         // round-robin within each tick
parallel.Add(TaskA()); parallel.Add(TaskB());
parallel.RunOn(runner);
```

Cannot `Add()` while running. Compiler-generated iterators do NOT support `Reset()` — for collections that reset, use custom enumerators.

### 5. Control pacing with flow modifiers

```csharp
runner.UseFlowModifier(new StaggeredFlow(5));        // max 5 tasks per Step()
runner.UseFlowModifier(new TimeBoundFlow(5f));       // max ~5ms per Step()
runner.UseFlowModifier(new TimeSlicedFlow(20f));     // fair round-robin time slices
```

`SerialFlow` runs one task at a time but does NOT guarantee start order of queued tasks.

## Hard rules and common mistakes

1. **Always `yield return Yield.It` inside loops.** Forgetting it blocks the runner in an infinite loop.
2. **Break semantics:**
   - `yield break` — stops this task permanently; parent continues.
   - `yield return Break.It` — stops this task; parent continues; **state machine stays alive (reusable/poolable)**.
   - `yield return Break.AndStop` — stops this task and every waiting `.Continue()` ancestor in its same-runner parent chain.
   - A pooled iterator resumes immediately after `Break.It`; it does not restart. Put the break at a complete cycle boundary, reset per-run state at the top of the enclosing infinite loop, and never pool a task stopped at another yield. The Lean pool return happens in `Dispose()` — automatic for runners, manual callers must call it.
3. **Primitive values use typed extraction.** Supported primitives (`int`, `uint`, `ulong`, `float`, `bool`) are stored inline in `TaskContract` without boxing. Read them with `.ToInt()` / `.ToUInt()` / `.ToUlong()` / `.ToFloat()` / `.ToBool()`; references use `.ToRef<T>()`.
4. **ExtraLean restrictions:** plain `IEnumerator` tasks may yield ONLY `null`, `Yield.It`, `Break.It`, `Break.AndStop`, or `yield break`. Anything else throws `SveltoTaskException`.
5. **Hold runner references.** Nothing else keeps runners alive — unreferenced runners get GC'd while tasks run. Always store and later `Dispose()` runners.
6. **Dispose disposes ALL tasks**, including queued ones that never ran.
7. **Threading:** `MultiThreadRunner` = one thread per runner instance. All tasks on one runner share that thread.
8. **MultiThreadRunner shutdown is cooperative.** `Flush()` and `Dispose()` wait up to two seconds for the worker, reject calls from that worker, and cannot abort a task stuck inside `MoveNext()`. `Flush()` keeps the worker reusable; `Dispose()` terminates it.

## Build and verify

```bash
dotnet build Svelto.Tasks.Tests.sln                  # builds everything (libs + tests + examples)

dotnet test Packages/com.sebaslab.svelto.tasks/Svelto.Tasks.Tests~/Svelto.Tasks.Tests.csproj
dotnet test Packages/com.sebaslab.svelto.common/Svelto.Common.Tests~/Svelto.Common.Tests.csproj

dotnet run --project Examples/01_GameLoop            # run any example
```

- SDK is pinned by `global.json`; library targets `netstandard2.1`, tests target `net9.0`, examples `net8.0` (both roll forward).
- Build artifacts go to custom `obj~/` / `bin~/` folders by repository convention. Don't be surprised; don't "fix" it.
- Tests are NUnit 4. There is no separate lint/format step — build warnings matter.

## Conventions for changes to this repo

- C# 10, `<Nullable>disable</Nullable>`, implicit usings disabled — write explicit usings, no nullable annotations.
- Log via `Svelto.Console` (`Log`, `LogWarning`, `LogError`, `LogDebug`) rather than direct console APIs.
- Contract checks use DBC (`Check.Require/Ensure/Assert`) — they compile away in release (`DISABLE_CHECKS`).
- Keep platform-specific code out of core paths and behind the existing platform defines.
- Every new feature should come with an example under `Examples/NN_Name/` following the existing pattern (csproj + Program.cs + README.md), and tests where practical.

## Deep reference docs

Read these before making non-trivial API changes or when unsure about semantics:

- `com.sebaslab.svelto.tasks/.aiguides/AI_GUIDE_Svelto.Tasks.md` — full Svelto.Tasks API reference: every type, every gotcha, patterns distilled from the test suite.
- `com.sebaslab.svelto.common/.aiguides/AI_GUIDE_Svelto.Common.md` — Svelto.Common data structures, logging, pooling.
- `Examples/README.md` — index mapping all 21 runnable examples to features. Each example folder is a minimal, working reference implementation of one feature.
