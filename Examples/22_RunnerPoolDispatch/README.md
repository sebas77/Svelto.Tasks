# Example 22: Runner Pool Dispatch (MultiThreadRunnerPool)

## Scenario

A server-style fan-out: **16 independent requests** are dispatched to a pool of
**4 background runners** (4 requests per runner, all in flight simultaneously).
Each request completes on its own; the host polls its own objects — the pool
provides no batch API.

## Feature Demonstrated

`MultiThreadRunnerPool` — a pool of independent ExtraLean `MultiThreadRunner`s.
`AddTask` is a **strict round-robin at submission time**: request *i* lands on
runner *i % threadCount*. No shared queue, no wrapper, no wave counter.

## When / Why Use It

- You have **independent root tasks** and roughly **tasks ≤ threads** — each task
  gets its own dedicated worker with the leanest possible hand-off (one atomic
  increment + a direct `AddTask`).
- You want fire-and-forget semantics: track completion yourself with your own
  counters/flags (as this demo does), no collective "all done" signal to pay for.
- You don't need rebalancing: if one task is much slower, *its* runner lags while
  others go idle — that is the trade for the lean dispatch.

## How It Works

1. Create the pool: `new MultiThreadRunnerPool("request-pool", 4, initialCapacity)`.
   Each inner runner owns one background thread for the pool's lifetime.
2. Submit root tasks: `request.RunOn(pool)` (ExtraLean `IEnumerator`).
3. Dispatch is deterministic round-robin — this demo records `i % numberOfRunners`
   per request and shows the resulting assignment table.
4. All requests run concurrently: each runner interleaves its four assignments,
   one `MoveNext()` per pass.
5. Completion is host-counted: each task `Interlocked.Increment`s a shared counter
   on its final `MoveNext`; the host polls until `completed == RequestCount`.
6. `pool.Dispose()` terminates every inner runner.

## Key Concepts

| Type | Namespace | Role |
|------|-----------|------|
| `MultiThreadRunnerPool` | `Svelto.Tasks.ExtraLean` | N independent runners, round-robin `AddTask` |
| `.RunOn(pool)` | `Svelto.Tasks.ExtraLean` | Submits an ExtraLean root task to the pool |
| `numberOfRunners` | (on pool) | Inner runner count, used here to compute the dispatch |
| `pool.Dispose()` | (on pool) | Terminates all inner workers |

## Gotchas

- **Round-robin, not work-stealing.** Assignment happens at submission and never
  rebalances. A slow task strands its runner's capacity; if that matters, use
  `MultiThreadedParallelTaskCollection` (Example 14), which feeds queued tasks to
  whichever runner goes idle.
- **No batch lifecycle.** No `Complete()`, no `onComplete`, no wave object. Count
  completions yourself (or poll per-task state) — for independent tasks this is
  often simpler than paying for batch machinery you don't use.
- **Root tasks only.** The pool rejects tasks with parent-spawned indices; runner-
  local continuation indices cannot be transferred between inner runners.
- **Threads live as long as the pool.** Same lifetime model as the parallel task
  collection — create it once, reuse it, dispose when done.
- When **tasks ≤ threads** this is the leaner tool; when **tasks > threads** the
  parallel task collection's self-balancing earns its machinery back (Example 14).
