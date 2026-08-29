# Example 14: Parallel Downloads (MultiThreadedParallelTaskCollection + IParallelTask)

## Scenario

**4 file downloads** running simultaneously on **4 threads**. Each "file" takes a
different amount of time. All progress bars advance concurrently — if they were
sequential it would take the sum of all durations; in parallel it takes the longest.

## Feature Demonstrated

`MultiThreadedParallelTaskCollection` (ExtraLean variant) with custom `IParallelTask`
implementations. Each task runs on its own thread and reports progress via a shared
state object.

## When / Why Use It

- You have **multiple independent async operations** (downloads, file I/O, network
  requests) that can run concurrently.
- Unlike `MultiThreadedParallelJobCollection` (which splits one job into index-slices),
  this collection runs **different tasks** on different threads.
- You want to poll progress from the main thread while work happens in the background.

## How It Works

1. Define a class implementing `IParallelTask` (which is `IEnumerator + IDisposable`):
   - `MoveNext()` returns `true` while the task is still running, `false` when done.
   - `Current` returns `null` (ExtraLean).
   - `Dispose()` for cleanup.
2. Create `new Svelto.Tasks.Parallelism.ExtraLean.MultiThreadedParallelTaskCollection(
   "name", threadCount, tightTasks)`.
3. Add tasks with `collection.Add(task)`.
4. Call `collection.Complete()` — runs synchronously until all tasks finish.
5. From the main thread (or a monitoring thread), poll shared progress state while
   `MoveNext()` runs the collection.

## Key Concepts

| Type | Namespace | Role |
|------|-----------|------|
| `IParallelTask` | `Svelto.Tasks.Parallelism` | `IEnumerator + IDisposable` interface for parallel tasks |
| `MultiThreadedParallelTaskCollection` | `Svelto.Tasks.Parallelism.ExtraLean` | Runs N tasks on M threads |
| `.Add(task)` | (on collection) | Registers a task |
| `.Complete()` | (extension) | Runs synchronously until all parallel tasks are done |
| `onComplete` | (event on collection) | Fired when all tasks finish |

## Gotchas

- **Constructor is `(name, threadCount, tightTasks)`** — `tightTasks: true` is for
  cache-friendly CPU-bound work; use `false` for I/O-bound tasks.
- Tasks are handed to a **pool of worker threads** as they free up: tasks are
  round-robin assigned from the runner pool at schedule time, and a thread that
  finishes early can claim more queued tasks. With fewer threads than tasks,
  tasks queue up.
- The collection's `MoveNext()` returns `true` while tasks are still running and
  `false` when all are done. Calling `Complete()` loops `MoveNext()` until done.
- **Don't add tasks while running** — it throws
  `MultiThreadedParallelTaskCollectionException`.
- Tasks must be **thread-safe** in any shared state they access (use `Interlocked` or
  `volatile`) — this includes the host's monitor loop: stop flags shared with
  other threads must be `volatile` (as `_monitoring` is here) or the final
  `Thread.Join()` can hang.
- The non-generic `MultiThreadedParallelTaskCollection` stores tasks in a non-generic
  list, so an `IParallelTask` implementing the interface through a `struct` is
  boxed when it is stored/scheduled; subsequent interface calls go through that
  boxed object. Use a **class** here, or the generic struct-constrained variant
  for value-type tasks.