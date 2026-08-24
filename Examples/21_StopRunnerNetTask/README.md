# Example 21: .NET Tasks Hosted on a Svelto Runner — TaskSynchronizationContext Lifecycle

## Scenario

An async method runs under a `TaskSynchronizationContext` attached to a `MultiThreadRunner`: every `await` continuation resumes on the runner's thread. Disposing the runner mid-await freezes the task forever, and its state machine becomes garbage-collectable once unreferenced.

## Feature

**Hosting .NET async methods on a Svelto runner via a SynchronizationContext.**

The context implements the standard .NET mechanism: an async method captures the ambient `SynchronizationContext` at each await and posts its continuation through it (`Post`). The context's pump — a plain svelto coroutine started on your runner — drains those continuations, so hosted code interleaves with your other tasks and runs on the runner's thread:

```csharp
var runner  = new MultiThreadRunner("BgWorker");
var context = new TaskSynchronizationContext(runner);

context.Run(async () =>
{
    await something;   // any suspension: Task.Yield, Task.Delay, I/O...
    DoWork();          // back on BgWorker — no RunOn sprinkled anywhere
});
```

## Why / When to Use

- You want existing `async`/`await` code to execute cooperatively on a specific runner thread (main/game loop or dedicated worker) instead of arbitrary ThreadPool threads.
- You need deterministic lifecycle semantics: stopping the runner abandons all hosted work.
- You want async continuations ordered together with your other Svelto coroutines.

## How It Works

```
context.Run(Job)
   │  Job starts synchronously on caller thread until first await
   ▼
await X  ──► continuation posted to context ──► _wait queue
                                                     │
                                    pump task (on YOUR runner, next tick)
                                                     ▼
                                  callback executed with context installed
                                                     ▼
                                  Job body continues on the runner thread
```

1. `Run(...)` installs the context while starting the method (first suspension captures it).
2. Each `Post` enqueues into `_wait`; the pump snapshots `_wait` → `_execute`, then executes callbacks with the context set as current (nested awaits recapture it).
3. Work posted during a drain is executed at the next tick — every await costs one hop.

## Key Concepts

| Type / Member | Namespace | Purpose |
|------|-----------|---------|
| `TaskSynchronizationContext(IGenericLeanRunner)` | `Svelto.Tasks.Lean` | Attaches the pump to **your** existing runner |
| `Run(Func<Task>)` / `Run<T>` | `Svelto.Tasks.Lean` | Hosts an async method; returns its Task handle |
| `Pump()` | internal | Infinite svelto coroutine draining posted continuations |

## Gotchas

- **Disposing the runner freezes all hosted tasks forever** — queued continuations are never invoked, nothing throws, nothing notifies. This is by design ("stopping means abandoning") and is what this example proves.
- The context itself roots queued continuations through its queues: for full garbage collection you must drop both the Task handle *and* the context reference.
- Code before the first `await` runs synchronously on the caller thread (standard .NET rule).
- Every `await` costs one pump tick of latency.
- `Send` executes inline on the calling thread — avoid it unless you are already on the right thread.
- Exceptions in continuations are logged via `Svelto.Console.LogException`, not rethrown.

## API Reference

```csharp
var context = new TaskSynchronizationContext(runner); // pump starts immediately

Task     t  = context.Run(SomeAsyncMethod);
Task<T>  tt = context.Run(SomeAsyncGenericMethod);

// lifecycle: kill everything hosted
runner.Dispose();
```
