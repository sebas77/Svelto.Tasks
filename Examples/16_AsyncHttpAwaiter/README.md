# Example 16: Async HTTP Awaiter — Awaiter Interop

## Scenario

Simulate an async HTTP request using the Svelto awaiter. A `SteppableRunner` ticks while an `async` method awaits `Task.Delay(...).RunOn(runner)`, showing the request being sent, the runner stepping, and the response arriving.

## Feature

**Awaiter interop** — bridging C# `async`/`await` into Svelto.Tasks.

The `.RunOn(runner)` extension method on `Task` (and `ValueTask`) returns a custom awaiter (`TaskRunnerAwaiter` / `ValueTaskRunnerAwaiter`) that posts the `async` continuation back onto the Svelto runner. This means code after `await` runs on the Svelto runner/thread, not the default synchronization context.

## Why / When to Use

- You have existing `async`/`await` code (e.g., HTTP requests via `HttpClient`) and want to integrate it into a Svelto.Tasks pipeline.
- You need the continuation after an `await` to run on a specific Svelto runner (e.g., a `SteppableRunner` ticked from a game loop).
- You want to mix `Task`-based async operations with iterator-based Svelto tasks on the same runner.

## How It Works

```
Task.Delay(800).RunOn(runner)
        │
        ▼
  TaskRunnerAwaiter
        │
        │  await suspends the async method
        ▼
  Task.Delay completes (on ThreadPool)
        │
        │  UnsafeOnCompleted() is called
        ▼
  ContinuationEnumerator posted to the runner
        │
        │  runner.Step() ticks it
        ▼
  async method resumes on the runner thread
```

1. `SimulateHttpRequest()` runs synchronously until the first `await`.
2. `Task.Delay(800).RunOn(runner)` creates a `TaskRunnerAwaiter` wrapping the real `TaskAwaiter`.
3. Because the task is not complete yet, `UnsafeOnCompleted` registers the hook on the `Task` itself — nothing runs on the runner yet.
4. The runner keeps stepping freely while `Task.Delay` ticks on the ThreadPool.
5. When the delay completes, the hook posts the async continuation onto the runner; the next `Step()` resumes the async method, which returns the response body — completing the returned `Task<string>`.
6. The main loop polls `task.IsCompleted`, so it exits as soon as the `Task` completes, having visibly stepped the runner N times.

## Key Concepts

| Type | Namespace | Purpose |
|------|-----------|---------|
| `Task.RunOn(SteppableRunner)` | `Svelto.Tasks.Lean` | Extension returning a `TaskRunnerAwaiter` |
| `ValueTask.RunOn(SteppableRunner)` | `Svelto.Tasks.Lean` | Extension returning a `ValueTaskRunnerAwaiter` |
| `TaskRunnerAwaiter` | `Svelto.Tasks.Lean` | Custom awaiter implementing `ICriticalNotifyCompletion` |
| `ContinuationEnumerator` | `Svelto.Tasks.Enumerators` | Pooled enumerator wrapping an `Action` continuation |
| `SteppableRunner` | `Svelto.Tasks.Lean` | Manually-stepped runner (`Step()`) |

## Gotchas

- **If the runner is killed before the awaited task completes, the continuation is deliberately never run.** `Task.IsCompleted` stays `false`: the delayed enqueue checks `runner.isValid` and skips instead of posting to a dead runner. (A merely stopped-but-reusable runner queues the continuation and runs it on the next tick.)
- The runner holds no strong reference to itself outside your code — always store the runner reference and `Dispose()` it.
- `Task.Delay` itself runs on the ThreadPool. Only the *continuation* (code after `await`) runs on the Svelto runner.
- The `ContinuationEnumerator` is pooled — it's recycled after use via `ContinuationEnumeratorPool`.

## API Reference

```csharp
// The extension method (Svelto.Tasks.Lean namespace):
public static TaskRunnerAwaiter RunOn(this Task task, SteppableRunner runner);

// Usage in an async method:
async Task DoWork(SteppableRunner runner)
{
    await Task.Delay(100).RunOn(runner);  // continuation posts to runner
    // ↑ this code runs on the runner when Step() is called
}
```