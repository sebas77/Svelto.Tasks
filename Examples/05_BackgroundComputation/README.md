# 05 · Background Computation — `RunOn()` + `MultiThreadRunner`

## Scenario

Heavy computation runs on a **background thread** via a `MultiThreadRunner` while
the main thread shows a spinner and polls the `Continuation`. When the background
task completes, the main thread prints the result.

This is the pattern for offloading expensive work (pathfinding, decompression,
asset loading) without freezing the main thread.

## Feature

- **`Lean.MultiThreadRunner`** — a runner that spins up a real background thread
  and processes queued tasks on it.
- **`.RunOn(bgRunner)`** — enqueues a task on a *specific* runner and returns a
  **`Continuation`** (a struct you can poll with `.isRunning`).
- The main thread loops, shows a spinner, and checks `continuation.isRunning`
  until it flips to `false`.

## When / Why to use it

- You have CPU-heavy work that would stall the main thread / game loop.
- You want a simple, allocation-aware alternative to `Task.Run` that integrates
  with Svelto's runner lifecycle.
- You need to run several background jobs on a **single** dedicated thread (the
  `MultiThreadRunner` processes its queue serially on one thread — create more
  runners for more threads).
- You want to `Stop()` / `Dispose()` the background work cleanly on shutdown.

## How it works

1. Create a `Lean.MultiThreadRunner` (it starts a background thread immediately).
2. Call `HeavyTask().RunOn(bgRunner)` — returns a `Continuation`.
3. The main thread loops: print spinner, check `cont.isRunning`, `Thread.Sleep`.
4. The background task yields `TaskContract.Yield.It` between chunks of work and
   reports progress via a shared volatile field.
5. When `cont.isRunning` is `false`, the main thread reads the result and prints
   it.
6. **Dispose** the runner to stop the background thread.

### The two-panel display

The console shows a left panel ("MAIN THREAD") with a spinning indicator and a
right panel ("BG THREAD") with a progress bar that fills as the background task
runs. When done, the result is printed.

## Key concepts

| Type / API | Purpose |
|---|---|
| `Svelto.Tasks.Lean.MultiThreadRunner` | Runs queued tasks on one dedicated background thread. |
| `.RunOn(runner)` | Enqueue on a runner; returns `Continuation` (Lean). |
| `Continuation.isRunning` | `true` while the task is still running. |
| `runner.WaitForTasksDone(timeout)` | Block the calling thread until the runner drains. |
| `runner.Dispose()` | Stop the background thread and clean up. |

## Gotchas

- **`MultiThreadRunner` runs on a real background thread.** Tasks must be
  thread-safe with respect to any shared state. Use `volatile` / `Interlocked` for
  cross-thread flags (as this example does for `progress` and `result`).
- **You MUST `Dispose()` the runner** to stop the background thread. If you let it
  be GC'd, the finalizer logs a warning and signals termination, but cannot wait
  for the worker. Dispose explicitly for deterministic cleanup.
- The runner processes its queue on **one** thread. If you queue 3 tasks they run
  **serially** on that thread, not in parallel with each other. For parallelism
  across threads, create multiple `MultiThreadRunner` instances or use
  `MultiThreadedParallelTaskCollection`.
- `Dispose()` rejects new work, signals terminal cleanup, and waits up to two
  seconds for the worker to exit. Shutdown is cooperative: if a task is stuck in
  an infinite loop or blocking call and never returns from `MoveNext()`, the
  worker cannot process cleanup and `Dispose()` throws `MultiThreadRunnerException`.
- `isRunning` on the `Continuation` flips to `false` the moment the task
  completes — there is no separate "collect the result" step. Read shared state
  right after.
- The `Continuation` is a pooled struct that returns itself to the pool automatically when the
  task completes (completion, break, stop, flush, dispose). **Do not call `ReturnToPool()` manually** —
  the pool has no duplicate-return guard and a double return can hand the same continuation to two tasks.
