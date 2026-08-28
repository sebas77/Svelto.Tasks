# Svelto.Tasks - AI Developer Guide

> **Note:** `AGENTS.md` (repo root) distills the essentials of this guide. When details conflict, this file is authoritative — update it first, then mirror into AGENTS.md.

> **Purpose:** A library to run serial and parallel asynchronous tasks (coroutines) in C#/Unity. Platform-agnostic core with Unity-specific extensions. Tasks are iterator-based (`IEnumerator`), not `async`/`await`-based, giving precise control over execution flow, scheduling, and threading.

## Architecture Overview

Svelto.Tasks is built on the concept of **iterators as tasks**. A task is any `IEnumerator` or `IEnumerator<TaskContract>` that runs one step per "tick" of a runner. The runner is the scheduler that calls `MoveNext()` on tasks according to a **flow modifier** strategy.

```
Task (IEnumerator<TaskContract>)
    ↓ registered on
Runner (IRunner) — schedules tasks
    ↓ uses
Flow Modifier (IFlowModifier) — controls iteration strategy
    ↓ calls MoveNext() on
ISveltoTask wrapper — drives the actual iterator
```

Key concepts:
- **Lean vs ExtraLean:** Two task "weights." Lean tasks yield `TaskContract` (rich: continuations, return values, breaks). ExtraLean tasks yield plain `IEnumerator` (minimal overhead, no continuations).
- **TaskContract:** The value type that Lean tasks yield. It's a discriminated union carrying values, continuations, break signals, or nested enumerators.
- **Flow Modifiers:** Strategies that control how a runner iterates its task list each tick (serial, staggered, time-bound, time-sliced).
- **Runners:** Schedulers that tick tasks. Steppable (manual), MultiThread (background thread), Sync (synchronous).
- **Continuations:** Handles that let a task wait for another task to complete.
- **TaskCollections:** Group multiple tasks into a single task that runs serially or in parallel.

---

## Namespace Map

| Namespace | Contains |
|-----------|----------|
| `Svelto.Tasks` | Core interfaces (`ISveltoTask`, `IRunner`), `TaskContract`, extensions |
| `Svelto.Tasks.Lean` | Lean task wrappers, steppable/multi-thread/sync runners for Lean |
| `Svelto.Tasks.ExtraLean` | ExtraLean task wrappers, runners for ExtraLean |
| `Svelto.Tasks.ExtraLean.Struct` | Struct-generic ExtraLean variants |
| `Svelto.Tasks.FlowModifiers` | Flow modifier implementations |
| `Svelto.Tasks.Internal` | Core engine (`SveltoTaskRunner`, `IFlowModifier`, `ContinuationPool`) |
| `Svelto.Tasks.Enumerators` | Enumerator utilities, `Continuation`, `WaitForSecondsEnumerator`, etc. |
| `Svelto.Tasks.Parallelism` | Parallel job/task collections, `ISveltoJob` |
| `Svelto.Tasks.Parallelism.Lean` / `.ExtraLean` | Parallel collection variants |

---

## 1. Core Abstractions

### `ISveltoTask`
The fundamental interface every runnable task implements. Deliberately NOT an `IEnumerator` to avoid ambiguity.
- `StepState Step(int runningTaskIndex, TombstoneHandle parentSpawnedTaskIndex)` — advance one step. Called by the runner.
- `void Stop()` — request the task to stop (completes on next step).
- `void Dispose()` — release resources.
- `bool isCompleted { get; }` — whether the task is done.
- `string name { get; }` — display name (profiling/debugging).

### `StepState` ([Flags] enum)
Result of a single `Step` call:
| Value | Meaning |
|-------|---------|
| `Invalid` | 0 (error) |
| `Running` | Task still running, continue next iteration |
| `Completed` | Task finished |
| `Faulted` | Task threw an exception |

### `TaskContract` (readonly struct)
**The central value type** that Lean tasks yield from `IEnumerator<TaskContract>.Current`. It's a discriminated union (internal `States` enum) that can carry:
- A **primitive value** (`int`, `uint`, `ulong`, `float`, `bool`, `string`) — return a value to the caller.
- A **reference** (`object`) — return a reference to the caller.
- An **exception** — propagate an error.
- A **continuation** — wait for another task.
- A **break signal** (`Break.It` / `Break.AndStop`) — stop the task.
- A **yield signal** (`Yield.It`) — yield execution to the next task/frame.
- A **continue signal** (`Continue.It`) — spawn a child task on the SAME runner as the parent and wait.
- A **nested enumerator** — run a child enumerator inline.
- A **fire-and-forget enumerator** — run a child but don't wait.

**Public constructors:** `(int)`, `(uint)`, `(ulong)`, `(float)`, `(bool)`, `(string)`, `(Exception)`.
**Implicit operators:** `int`, `long`, `float`, `Continuation`, `Break`, `Yield`, `Continue`, `string` → `TaskContract`.
**Extraction methods:** `ToInt()`, `ToUlong()`, `ToFloat()`, `ToBool()`, `ToRef<T>()`, `FromReference(object)`.

### `TaskContract.Yield`
Sentinel for yielding one frame/iteration. Usage: `yield return TaskContract.Yield.It;` (or just `yield return Yield.It;` with `using static`).

### `TaskContract.Continue`
Sentinel to continue a child task on the **same runner** as the parent. Usage: `yield return someEnumerator.Continue();`

### `TaskContract.Break`
Sentinel to break out of a task loop.
- `Break.It` — breaks the task but NOT the caller. The enumerator can be reused (state machine kept alive). Enables the `while(true) { yield return Break.It; }` pattern for reusable iterator blocks.
- `Break.AndStop` — breaks the task and every waiting `.Continue()` ancestor in its same-runner parent chain.
- `AnyBreak` — true if `It` or `AndStop`.

**Key distinction:** `yield return Break.It` keeps the state machine alive (can be reused). `yield break` ends the state machine permanently. `Break.AndStop` propagates the break to the parent.

### `SveltoTaskException`
Standard exception type for Svelto.Tasks errors. Constructors: `(string)`, `(Exception)`, `(string, Exception)`.

---

## 2. Lean vs ExtraLean Tasks

### Lean Tasks (`IEnumerator<TaskContract>`)
- Yield `TaskContract` — can return values, start continuations, break, yield, spawn children.
- Supported by: `SteppableRunner`, `MultiThreadRunner`, `SyncRunner` (Lean variants).
- Wrapped by: `LeanSveltoTask<TTask>`.
- Run via: `enumerator.RunOn(runner)` → returns `Continuation`.
- Can be awaited: `enumerator.ToTask<T>()` → `ValueTask<T>`.

### ExtraLean Tasks (`IEnumerator`)
- Yield only `null`, `Yield.It`, `Break.It`, `Break.AndStop`, or `yield break`. No return values, no continuations.
- Minimal overhead — no `TaskContract` processing.
- Supported by: `SteppableRunner`, `MultiThreadRunner`, `SyncRunner` (ExtraLean variants).
- Wrapped by: `ExtraLeanSveltoTask<TTask>` (class or struct variant).
- Run via: `enumerator.RunOn(runner)`.

**When to use which:**
- **Lean:** When you need return values, continuations, breaks, or the full `TaskContract` feature set.
- **ExtraLean:** When you want minimal overhead and just need a simple coroutine that yields until done.

---

## 3. Runners

### `IRunner` / `ISteppableRunner` / `IRunner<T>`
Interface hierarchy:
- `IRunner : IDisposable` — base marker.
- `ISteppableRunner : IRunner` — manually stepped. `bool Step()`, `bool hasTasks { get; }`.
- `IRunner<T> : IRunner where T : ISveltoTask` — accepts a specific task type. `void AddTask(in T, (int, TombstoneHandle) index)`.

### `SteppableRunner`
A manually-stepped runner. You call `Step()` each frame/tick. Uses `StandardFlow` by default.
- `Step()` → `bool` — ticks all tasks once. Returns false if no tasks remain.
- `Pause()` / `Resume()` — freeze/unfreeze task processing.
- `Stop()` — marks running tasks for cancellation on the next step; queued tasks wait until automatic unstop.
- `Flush()` — synchronously disposes running and queued tasks, then allows reuse.
- `Reset()` — equivalent to `Flush()` for all SteppableRunner variants.
- `UseFlowModifier<TFlowModifier>()` — set the flow modifier strategy.
- Implements `IEnumerator`/`IEnumerator<TaskContract>` itself, so it can be yielded as a task inside another runner.

**Variants:**
- `Svelto.Tasks.Lean.SteppableRunner` — for `IEnumerator<TaskContract>`.
- `Svelto.Tasks.Lean.SteppableRunner<T>` — for specific `T : IEnumerator<TaskContract>`.
- `Svelto.Tasks.ExtraLean.SteppableRunner` — for `IEnumerator`.
- `Svelto.Tasks.ExtraLean.SteppableRunner<T>` — for `T : struct, IEnumerator`.

**When to use:** When you want manual control over when tasks tick. In Unity, call `Step()` from an `Update()` method or via `PlayerLoopUtility`. The runner itself can be yielded into another runner for composition.

### `MultiThreadRunner`
Runs tasks on a **dedicated background thread**. One thread per runner (all tasks on a runner share the same thread).
- **Constructors:** `(string name, bool relaxed, bool tightTasks, uint initialNumberOfTasks)` or `(string name, uint intervalInMs, uint initialNumberOfTasks)`.
  - `relaxed: true` — less reactive to new tasks, lower CPU.
  - `tightTasks: true` — the worker stops volunteering periodic yields; use it for cache-friendly tight loops that should own the thread.
  - `intervalInMs` — starts a low-CPU runner that ticks at the given interval.
  - `initialNumberOfTasks` — pre-sizes the internal task containers to avoid growth allocations.
- Uses a "quick locking" spin mechanism for reactive pause/resume.
- `Pause()` / `Resume()` — thread-safe.
- `Stop()` — asynchronously cancels running tasks; the worker remains alive and queued tasks run after automatic unstop.
- `Flush()` — synchronously disposes running and queued tasks, rejects submissions during cleanup, and keeps the worker alive for reuse.
- `Dispose()` — terminally rejects submissions, disposes all tasks, signals the worker to exit, and joins it.
- `Flush()` and `Dispose()` cannot be called from the runner's worker thread and have a two-second safety timeout.

**Variants:** Same as SteppableRunner (Lean/ExtraLean/struct/class).

**When to use:** When tasks should run on a background thread (e.g., parallel computation). Create multiple `MultiThreadRunner` instances for separate threads.

### `SyncRunner`
A synchronous runner that completes tasks immediately. Subclass of `SteppableRunner`.
- `Step()` runs tasks to completion synchronously.
- `LocalSyncRunners` / `LocalSyncRunners<T>` — thread-local singletons used by `Complete()`.

**When to use:** Rarely used directly. The `Complete()` extension method uses it internally to run a task synchronously on the current thread.

### `GenericSteppableRunner<TTask>`
Base class for steppable runners. Holds a `FlushingOperation` (pause/stop/kill/reset state machine) and an `IProcessSveltoTasks<TTask>` processor.

**Warning from source:** "unless you are using the StandardSchedulers, nothing holds your runners. Be careful that if you don't hold a reference, they will be garbage collected even if tasks are still running."

### Task exception strategy
When a task throws, the runner marks it `Faulted`, invokes `TaskExceptionStrategy.Current`, disposes and removes the
faulted task, then continues ticking the remaining tasks. The strategy is a mandatory process-wide
`ITaskExceptionStrategy`; implementations must be thread-safe because multiple multithreaded runners may report
exceptions concurrently.

The built-in `LogTaskExceptionStrategy` preserves the standard behavior by forwarding exceptions to
`Console.LogException`. Assign a custom strategy to report faulted tasks to an external service such as Sentry:

```csharp
TaskExceptionStrategy.Current = new MyTaskExceptionStrategy();
```

`HandleException` is a notification hook and does not alter `StepState` or runner control flow.
Exceptions escaping a custom strategy are ignored so a reporting failure cannot interrupt faulted-task cleanup or
stop the runner.

---

## 4. Flow Modifiers

Flow modifiers control how a runner iterates its task list each `MoveNext` tick. Set via `runner.UseFlowModifier<TFlowModifier>()`.

### `IFlowModifier`
- `bool CanMoveNext<T>(ref int nextIndex, int coroutinesCount, bool hasCoroutineCompleted)` — decide whether to continue to the next task in the same tick.
- `bool CanProcessThis(ref int index)` — gate before stepping a specific task.
- `void Reset()` — called at the start of each tick.

### `StandardFlow` (default)
Processes all tasks, always moves next. No restrictions.

### `SerialFlow`
Guarantees tasks run one at a time (serially), but order is NOT guaranteed. Stays on the current task until it completes.
- **Warning:** "if you use serial flow, a root task cannot wait for another root task." Use `.Continue()` instead of `.RunOn(runner)` for same-runner tasks.

### `StaggeredFlow`
Runs at most `maxTasksPerIteration` tasks per tick. Constructor: `(int maxTasksPerIteration)`.
- TaskCollections count as a single task.

**When to use:** When you have many tasks and want to limit how many run per frame to avoid spikes.

### `TimeBoundFlow`
Limits total tick duration to `maxMilliseconds`. Uses `Stopwatch`. Stops processing when elapsed exceeds the bound.
- Constructor: `(float maxMilliseconds)`.

**When to use:** When you need to bound the total time spent on tasks per frame (e.g., "process tasks for at most 5ms per frame").

### `TimeSlicedFlow`
Like `TimeBoundFlow` but when the end of the task list is reached while time is still left
in the slice, `nextIndex` wraps back to 0 instead of ending the tick: all tasks are revisited
several times within the same `Step()` until the budget expires.
- Constructor: `(float maxMilliseconds)`.

**When to use:** Fair revisiting of many never-completing tasks within a wall-clock budget.
Note this is possible only because `CanMoveNext` is evaluated before the out-of-bound check
in the scheduler loop (see `SveltoTaskRunner`).

---

## 5. Task Operations (Extensions)

### Running Tasks
```csharp
// Lean: returns Continuation (can check isRunning)
Continuation c = enumerator.RunOn(runner);
yield return c;  // wait for it

// ExtraLean: no return value
enumerator.RunOn(runner);
```

### `.Continue()`
Spawns a child task on the **same runner** as the parent and waits for it.
```csharp
yield return childEnumerator.Continue();
```
**Important:** Use `.Continue()` (not `.RunOn(runner)`) when the child should run on the same runner as the parent. `.RunOn()` returns a `Continuation` handle; `.Continue()` spawns inline.

### `.Forget()`
Fire-and-forget: runs a task on the same runner but the caller does NOT wait.
```csharp
yield return backgroundTask.Forget();
```

### `.Complete()`
Runs a task synchronously to completion on a thread-local `SyncRunner`.
```csharp
enumerator.Complete();  // blocks until done
enumerator.Complete(timeoutMs: 5000);  // with timeout
```
Also works on `ISteppableRunner`: `runner.WaitForTasksDone()` / `WaitForTasksDoneRelaxed()`.

### `.ToTask<T>()` (Lean only)
Converts a Lean iterator into an awaitable `ValueTask<T>` that polls the continuation on the thread pool. Requires a runner and `T` must be a reference type (`TaskContract` cannot box a generic value).
```csharp
string result = await enumerator.ToTask<string>(runner);
```

---

## 6. Continuations

### `Continuation` (readonly struct)
A handle returned by `RunOn` that lets the caller check if a task is still running.
- `bool isRunning { get; }` — checks if the task is still active (uses an incrementing generation token to detect stale handles).
- `void ReturnToPool()` — return the continuation to the pool.

**Lifecycle:** a Lean task returns its continuation to the pool automatically when the task is disposed (completion, break, stop, flush, dispose). Never call `ReturnToPool()` manually after completion: the pool has no duplicate-return guard and a double return can hand the same internal continuation to two tasks.

### `ContinuationPool` (internal, static)
Pre-allocates 1000 `ContinuationEnumeratorInternal` objects. Uses `GC.ReRegisterForFinalize`/`GC.SuppressFinalize` so continuations return themselves to the pool when GC'd.

### `ContinuationEnumerator`
A pooled Lean enumerator that runs a single `Action` continuation then completes. Used by the awaiter infrastructure to post `async` continuations onto a runner.

---

## 7. Task Collections

Collections group multiple tasks into a single `IEnumerator<TaskContract>` task.

### `TaskCollection<T>` (abstract base)
- `Add(in T enumerator)` — add a root task (cannot add while running).
- `onException` event (`Func<Exception, bool>`) — exception handler. Return `true` to complete, `false` to re-throw.
- `isRunning { get; }`, `Clear()`, `Reset()`.
- Supports nested task continuation (pushing child enumerators onto a stack).
- `RunTasksAndCheckIfDone()` — abstract, implemented by serial/parallel.

### `SerialTaskCollection`
Runs child tasks one after another (serially). Advances to the next task only when the current one completes.
- `SerialTaskCollection` — non-generic convenience.
- `SerialTaskCollection<T>` — generic where `T : IEnumerator<TaskContract>`.
- `StackTask<T>` — single-task helper with `Reset(IEnumerator<TaskContract>)`.

**When to use:** When tasks must run in sequence (e.g., load A, then B, then C).

### `ParallelTaskCollection`
Runs all child tasks "in parallel" within a single tick — each task runs synchronously until it yields, then the next task runs. Round-robins through tasks each tick. Completed tasks are swap-removed.
- `ParallelTaskCollection` — non-generic convenience.
- `ParallelTaskCollection<T>` — generic where `T : class, IEnumerator<TaskContract>`.

**When to use:** When tasks can run concurrently and yield cooperatively. Each task gets a chance to run each tick.

---

## 8. Parallelism (Multi-Threaded)

### `ISveltoJob`
Interface for a parallelizable job (Unity-Jobs-like). `T : struct` not required but common.
- `void Update(int jobIndex)` — do one iteration's work.
- Inherits `IDisposable`.

### `MultiThreadedParallelTaskCollection`
Runs a collection of `IParallelTask` across N real OS threads (one `MultiThreadRunner` per thread). Tasks are distributed round-robin.
- Default thread count: `Math.Max(1, Environment.ProcessorCount - 2)`.
- `Add(IParallelTask)`, `MoveNext()`, `Stop()`, `Dispose()`.
- `onComplete` event — fired when all tasks done.
- `isRunning { get; }`.

**Variants:**
- `Lean.MultiThreadedParallelTaskCollection` / `<TTask>` — for `IEnumerator<TaskContract>` tasks.
- `ExtraLean.MultiThreadedParallelTaskCollection` / `<TTask>` — for `IEnumerator` tasks.

### `IParallelTask`
- Inherits `IEnumerator, IDisposable`.

### `MultiThreadedParallelJobCollection<TJob>`
Splits a single `ISveltoJob` struct into `iterations` slices distributed across threads (Unity-Jobs-like).
- `Add(in TJob job, int iterations)` — computes `tasksPerThread` and remainder, creates `ParallelRunEnumerator<TJob>` slices.
- `TJob : struct, ISveltoJob`.

**When to use:** Data-parallel work (e.g., process 10000 items across 8 threads). Like Unity Jobs but without the Jobs system dependency.

### `ParallelRunEnumerator<T>`
The enumerator that runs a slice of an `ISveltoJob` (from `startIndex` for `numberOfIterations`). `MoveNext` runs the whole slice synchronously then returns false.

---

## 9. Enumerators (Ready-to-Use Tasks)

### Timing
#### `WaitForSecondsEnumerator`
Yields `TaskContract.Yield.It` until a number of seconds have elapsed. Class (allocates).
- Constructor: `(float seconds)`. `Reset(float seconds)`.

#### `ReusableWaitForSecondsEnumerator`
Struct version (no allocation, reusable).
- `Reset(float seconds)`, `IsDone()`.

### Signaling
#### `WaitForSignal<T>`
Abstract base for cross-thread signaling. Self-referential generic (`T : WaitForSignal<T>`) forces named subclasses.
- `WaitForSignal(string name, float timeout = 1000, bool autoreset = true, bool startUnlocked = false)`.
- `Signal()` / `SignalBack()` — signal from another thread.
- `Wait()` / `WaitBack()` → `IEnumerator` — yield until signaled.

**When to use:** Cross-thread synchronization where one thread waits for a signal from another.

#### `WaitForState<T, W>`
Abstract state-machine waiter. Subclass with an enum `W` of states.
- `Generate()` → `WaitForEnumerator` — blocks until the state matches a target.
- `SignalStateChange(W newState)` — update the state.

### Action Wrappers
#### `LocalFunctionEnumerator` / `LocalFunctionEnumerator<T>`
Lean struct enumerators wrapping `Func<bool>` / `FuncRef<T,bool>`. Runs until the function returns false. Zero-allocation.

#### `SmartFunctionEnumerator<TVal>`
Lean enumerator wrapping `FuncRef<TVal,bool>`. The function controls flow via its return value and can carry a counter by ref.

#### `InterleavedLoopActionEnumerator`
ExtraLean enumerator that runs an `Action` at a fixed interval (ms). Never completes (always returns true from `MoveNext`).

#### `TimedLoopFunctionEnumerator`
ExtraLean enumerator wrapping `Func<float,bool>` where the float is elapsed seconds since last yield.

### Unity-Specific
#### `YieldInstructionEnumerator`
Bridges Unity `YieldInstruction`/`AsyncOperation` into a Svelto.Tasks enumerator by starting a real Unity coroutine internally.
- Constructors: `(YieldInstruction)`, `(AsyncOperation)`.

#### `UnityWebRequestEnumerator`
Wraps a `UnityWebRequest`, completing when `isDone`.
- Constructor: `(UnityWebRequest www, int timeOutInSeconds = -1)`.

---

## 10. Iterator Block Pooling

### Lean: `IteratorBlockPool<P>` / `PooledIteratorBlock<T>`
Pools iterator blocks for reuse. `P` is a data class, `PooledIteratorBlock<P>` wraps it as `IEnumerator<TaskContract>`.

**The reusable iterator pattern:**
```csharp
IEnumerator<TaskContract> MyReusableTask()
{
    while (true)  // infinite loop — state machine never ends
    {
        // ... do work ...
        yield return TaskContract.Break.It;  // signals end, but state machine stays alive
    }
}
```
`Break.It` signals the task is done for now, but the state machine is NOT ended (unlike `yield break`). The pooled iterator block can be reused with new data via `IteratorBlockPool<P>.Get()`.

**Design reusable blocks around explicit cycle boundaries.** Reuse resumes immediately after the `yield return Break.It`; it does not restart the method. The code after that yield must safely reach the next loop iteration, per-run locals must be reset at the top of the loop, and the pooled data object must be re-initialized after every `Get()`. Do not carry borrowed references or resources across the break. `yield break`, natural completion, or cancellation before the break leaves the state machine at a location that is not safe to pool.

### ExtraLean: `IteratorBlockPool<T>` / `PooledIteratorBlock<T>`
Same concept but for plain `IEnumerator`.

**When to use:** When you run the same task repeatedly and want to avoid allocating a new iterator each time. Use the `while(true) { yield return Break.It; }` pattern.

**Threading & disposal contract (both Lean and ExtraLean):**
- `Get()` and `Return()` are thread safe: both pool variants use `ThreadSafeStack`, so a block may be acquired and returned by different runner threads.
- A borrowed block and its data object remain exclusively owned from `Get()` until `Return()`. Do not concurrently advance, mutate, or return the same borrowed block from multiple threads.
- Reaching the designated `Break.It` boundary is what makes a borrowed block reusable. A task stopped at any other yield must not be returned and resumed as a new cycle.
- Lean ownership: `MoveNext()` only flags a break-completed block; the pool return happens in `PooledIteratorBlock.Dispose()`, which runners call automatically on task completion. Manual callers must call `Dispose()` themselves. Blocks that complete naturally or are abandoned mid-cycle are disposed permanently and left to the GC — they are never pooled.
- `Dispose()` drains idle blocks only. Stop the runners and wait for every borrowed block to return before disposing the pool; concurrent disposal and use is unsupported.

---

## 11. MultiThreadRunnerPool

### `MultiThreadRunnerPool` (ExtraLean)
A pool of independent ExtraLean `MultiThreadRunner`s. Each scheduled task is a root task dispatched round-robin to one of the inner runners. Runner-local continuation indices cannot be transferred between different inner runners, so this pool is intended for independently scheduled root tasks.
- Constructor: `(string name, int threadCount, uint initialNumberOfTasks, ...)`.
- `AddTask(...)` / `Stop()` dispatch to the next inner runner; `Dispose()` disposes every inner runner.
- The owner must guarantee that no `AddTask` or `Stop` calls overlap disposal.

**When to use:** When many independent background tasks should share a fixed set of worker threads instead of one runner per concern.

---

## 12. Awaiter Support (async/await interop)

### `SveltoAwaiterExtensions`
Extension methods that let `.NET Tasks` and `ValueTasks` be awaited through a Svelto runner:
- `Task.RunOn(IGenericLeanRunner)` — returns a `TaskRunnerAwaiter`.
- `ValueTask.RunOn(IGenericLeanRunner)` — returns a `ValueTaskRunnerAwaiter`.

### `TaskRunnerAwaiter` / `ValueTaskRunnerAwaiter`
Custom awaiters (`ICriticalNotifyCompletion`) that wrap the real `TaskAwaiter`/`ValueTaskAwaiter`. When the awaited operation completes, the async continuation is enqueued on the chosen Svelto runner, so code after the `await` runs on the runner's thread, interleaved with its other tasks. If the runner is no longer valid at completion time, the continuation is deliberately never run and the async task stays pending.

**When to use:** When you need to interop with `async`/`await` code while keeping continuation affinity to a Svelto runner. Note the direction: these awaiters bring .NET Tasks *into* Svelto; use `ToTask<T>()` to bring iterators out to .NET.

---

## 13. Profiler

### `TaskInfo`
Struct holding profiling info about a task: `name`, `frameCount`, `duration`, `type`.

### `TaskProfiler` and `ITaskProfilerDriver` (optional)
Enabled by the `TASKS_PROFILER_ENABLED` define. `TaskProfiler` measures every task `Step` duration and runner processing scope without requiring a platform-specific dependency. It keeps the existing `TaskInfo` data and forwards balanced scopes to the optional `TaskProfiler.Driver`.

`ITaskProfilerDriver` is the platform-agnostic extension point:
- `BeginRunner` / `EndRunner` wrap a runner processing pass.
- `BeginTask` / `EndTask` wrap an individual task step; `EndTask` receives its elapsed milliseconds.

### `UnityTaskProfilerDriver` (Unity)
Installs itself as the profiler driver through `RuntimeInitializeOnLoadMethod` whenever `TASKS_PROFILER_ENABLED` is compiled in; manual assignment overrides it:

```csharp
TaskProfiler.Driver = new UnityTaskProfilerDriver();
```

It uses the `Svelto.Tasks` Unity Profiler category and emits:
- dynamic `ProfilerMarker` samples for runners and normalized task names, including background runners;
- `Task Time` (nanoseconds) and `Task Steps` per-frame counters, for the native Profiler chart.

`com.unity.profiling.core` is the Unity package dependency that provides the counter API. Counter updates are synchronized because Svelto runners can execute on multiple threads.

### `SveltoTasksProfilerModule` (Unity Editor)
Unity automatically discovers this module. It enables the `Svelto.Tasks` category, charts the two aggregate counters, and provides a CPU-module-style details view:
- a runner picker listing one entry per runner scope that ran (identified by the `Runner/` marker prefix; busiest first). Threads without runner scopes fall back to a thread entry. The picker resolves each runner to its thread internally, so threads are never exposed;
- a filter box: case-insensitive substring on normalized scope names; when set it spans every active runner/thread and disables the picker;
- an expandable Object / Total / Self / Calls / GC Alloc tree built from the merged call hierarchy (`HierarchyFrameDataView`), pruned so only Svelto subtrees are shown — non-Svelto branches never reach the view;
- Total cells show their share of visible time; dominant branches (>50%) are red-tinted.
Task names are normalized (`Type.Method` for iterators; wrapper enumerators resolve to the full path of the wrapped task type).

---

## 14. DBC (Design By Contract)

### `DBC.Tasks.Check` (static)
Same pattern as `Svelto.Common.DBC.Common.Check` but in the `DBC.Tasks` namespace. Precondition/postcondition/invariant/assertion checks. All compile away in release (`DISABLE_CHECKS`).

---

## 15. Internal Engine

### `SveltoTaskRunner<TSveltoTask>` (internal, static)
The core scheduling engine. Contains:
- `Process<TFlowModifier>` — the actual `MoveNext` loop. Maintains:
  - `_newTaskRoutines` (ConcurrentQueue) — tasks queued but not yet running. ConcurrentQueue because `StartTask` may be called from another thread.
  - `_runningCoroutines` (FasterList<TombstoneHandle>) — indices of currently-running (leaf) tasks.
  - `_spawnedCoroutines` (TombstoneList) — ALL spawned tasks (root + children). Only leaves run.
- `FlushingOperation` — thread-safe state machine for pause/stop/kill/reset.

**Key design notes from source:**
- "the difference between stop and pause is that pause freezes the task states, while stop flushes them until there is nothing to run. Ever looping tasks are forced to be stopped and therefore can terminate naturally"
- "a stopped runner can restart, and the design allows queueing new tasks in the stopped state, although they won't be processed"
- `StartTask` may be called from a different thread → `_newTaskRoutines` is a `ConcurrentQueue`

### `FlushingOperation`
Thread-safe bitmask state for runner lifecycle. State transitions use compare-exchange loops so concurrent transitions cannot lose the terminal kill flag or its reset/stop wake-up flags:
- `Pause()` / `Resume()` — freeze/unfreeze.
- `Stop()` / `Unstop()` — flush tasks until empty, then allow restart.
- `StopAndReset()` — flush and clear for reuse.
- `Kill()` — internal terminal signal; cleanup and worker termination happen cooperatively on the worker's next processing pass.
- `acceptsNewTasks` — true only when not paused/stopped/killed.

---

## Quick Reference: Common Patterns

### Run a task on a steppable runner (Unity)
```csharp
var runner = new SteppableRunner("MyRunner");
// In Update():
runner.Step();
```

### Run a task on a background thread
```csharp
var runner = new MultiThreadRunner("BgRunner", relaxed: true, tightTasks: false);
myTask.RunOn(runner);
// remember to hold a reference to runner and dispose it when done
```

### Wait for a task from another task
```csharp
IEnumerator<TaskContract> ParentTask()
{
    var cont = childTask.RunOn(otherRunner);
    yield return cont;  // wait for child
}
```

### Continue a child on the same runner
```csharp
IEnumerator<TaskContract> ParentTask()
{
    yield return ChildTask().Continue();  // runs on same runner, parent waits
}
```

### Fire and forget
```csharp
IEnumerator<TaskContract> ParentTask()
{
    yield return BackgroundWork().Forget();  // runs but parent doesn't wait
}
```

### Run synchronously
```csharp
myTask.Complete();  // blocks until done
myTask.Complete(timeoutMs: 5000);  // with timeout
```

### Reusable iterator block
```csharp
IEnumerator<TaskContract> ReusableWork()
{
    while (true)
    {
        // do work with pooled data
        yield return TaskContract.Break.It;  // done for now, but reusable
    }
}
```

### Parallel job
```csharp
var jobCollection = new MultiThreadedParallelJobCollection<MyJob>();
jobCollection.Add(myJob, iterations: 10000);
jobCollection.RunOn(parallelRunner);
```

### Serial task collection
```csharp
var serial = new SerialTaskCollection();
serial.Add(TaskA());
serial.Add(TaskB());
serial.Add(TaskC());
serial.RunOn(runner);  // A → B → C
```

### Parallel task collection
```csharp
var parallel = new ParallelTaskCollection();
parallel.Add(TaskA());
parallel.Add(TaskB());
parallel.Add(TaskC());
parallel.RunOn(runner);  // all run concurrently, yielding to each other
```

### Time-bound flow
```csharp
var runner = new SteppableRunner("Bounded");
runner.UseFlowModifier<TimeBoundFlow>();  // won't exceed default time
// or: new TimeBoundFlow(5.0f) for 5ms max per tick
```

---

## Quick Reference: Choosing a Runner

| Need | Use |
|------|-----|
| Manual tick control (Unity Update) | `SteppableRunner` |
| Background thread | `MultiThreadRunner` |
| Synchronous completion | `SyncRunner` / `.Complete()` |
| Parallel across N threads | `MultiThreadedParallelTaskCollection` |
| Data-parallel job | `MultiThreadedParallelJobCollection<TJob>` |
| Serial task sequence | `SerialTaskCollection` |
| Concurrent task group | `ParallelTaskCollection` |

## Quick Reference: Choosing a Flow Modifier

| Need | Use |
|------|-----|
| Default (all tasks per tick) | `StandardFlow` |
| One at a time | `SerialFlow` |
| Limit tasks per tick | `StaggeredFlow(n)` |
| Bound total tick time | `TimeBoundFlow(ms)` |
| Fair time-slicing | `TimeSlicedFlow(ms)` |

---

## Practical Patterns & Gotchas (from tests)

### Continue() vs RunOn() vs Forget() — the critical distinction

| Method | Returns | Parent waits? | Runs on | Use when |
|--------|---------|---------------|--------|----------|
| `.Continue()` | `TaskContract` | **Yes** | Same runner as parent | Child should run on the same runner and parent must wait |
| `.RunOn(runner)` | `Continuation` | No (must poll `.isRunning`) | Specified runner | Child runs on a different runner; parent polls or yields the continuation |
| `.Forget()` | `TaskContract` | **No** | Same runner (scheduled) | Fire-and-forget; parent continues immediately |

**Key gotcha:** `Forget()` causes the parent to continue **before** the child's body executes. Execution order is `[1, 4, 2, 3]` not `[1, 2, 3, 4]`:
```csharp
IEnumerator<TaskContract> Parent() {
    order.Add(1);
    yield return Child().Forget();  // child scheduled, parent continues
    order.Add(4);                    // runs BEFORE child's body (order: 1, 4, 2, 3)
}
```

### Break.It vs Break.AndStop vs yield break

| Construct | Current task | `.Continue()` ancestors | Unrelated roots |
|-----------|-------------|------------|----------|
| `yield break` | Stops | **Continues** | Continue |
| `Break.It` | Stops | **Continues** | Continue |
| `Break.AndStop` | Stops | **Stop** | Continue |

- `yield break` and `Break.It` behave identically when the breaking task is yielded via `.Continue()`. The parent continues in both cases.
- `Break.AndStop` stops every waiting `.Continue()` ancestor, so none reach a subsequent
  `yield return` statement. Unrelated root tasks continue running.
- `Break.It` keeps the state machine alive (reusable via iterator block pooling). `yield break` ends it permanently.

### TaskContract: yielding and extracting typed values
```csharp
IEnumerator<TaskContract> SubEnumerator(int i, int total) {
    do { yield return TaskContract.Yield.It; } while (++i < count);
    yield return i;  // int is stored inline in TaskContract; retrieve via .ToInt()
}
// To read the result after continuation:
yield return subEnumerator.Continue();
yield return subEnumerator.Current;  // passes the value up
int result = testEnum.Current.ToInt();
```
- `yield return i;` invokes the typed `TaskContract(int)` conversion and stores the value in the contract's inline union without boxing. Use `.ToInt()` to extract it; `.ToRef<T>()` is for reference payloads only.
- `yield return TaskContract.Yield.It;` is required inside loops to enable asynchronous execution. **Forgetting it causes an infinite loop** that blocks the runner.

### TaskContract.Continue.It as a return value (not yield)
```csharp
TaskContract TestContinuation(int i) {
    switch (i) {
        case 0: return TestEnum().Continue();     // continue another task
        case 1: return TaskContract.Continue.It;   // immediate MoveNext (no yield)
        case 2: return AnotherTask().Continue();
    }
}
```
`TaskContract.Continue.It` triggers an immediate `MoveNext` on the current task without yielding — the task keeps running in the same tick.

### Deep continuation chains (32+ levels)
Tests confirm that 32 nested `.Continue()` calls work correctly even when the runner's internal list resizes. The `SveltoTaskWrapper` struct is fully set before `SpawnContinuingTask` is called (which may trigger a resize), so the `this` reference stays valid.

### TaskContract can return another TaskContract
```csharp
IEnumerator<TaskContract> FirstEnum() {
    yield return TaskContract.Yield.It;
    var testEnum1 = SecondEnum();
    yield return testEnum1.Continue();
    yield return testEnum1.Current;  // value bubbles up through the chain
}
```

### Runner lifecycle: Pause, Stop, Flush, Dispose

| Operation | Tasks execute? | New tasks accepted? | Runner reusable? |
|-----------|---------------|--------------------|--------------------|
| `Pause()` | No (frozen) | Yes (queued) | Yes (after `Resume()`) |
| `Resume()` | Yes | Yes | Yes |
| `Stop()` | Cancels in-flight tasks on the next pass | Yes (queued until automatic unstop) | Yes |
| `Flush()` | No; all tasks are disposed | No during cleanup | Yes; worker is retained |
| `Dispose()` | No; all tasks are disposed | No; submission throws | No; worker exits |

**Stop behavior:** After `Stop()`, tasks can still be `RunOn`'d. They remain queued while running tasks are cancelled, then process after the runner automatically unstops. `Stop()` itself does not wait for this cleanup.

**Flush and Dispose dispose ALL live tasks** — queued or running. Even tasks that never started get disposed. Completed tasks were already disposed when they completed.

**Runner GC warning:** Runners can be garbage collected if not referenced. The framework does NOT keep a reference. Always store runner references and dispose them explicitly.

### MultiThreadRunner specifics
- `new MultiThreadRunner("name")` — starts immediately, uses default spinning.
- `new MultiThreadRunner("name", relaxed: true, tightTasks: false)` — relaxed: less reactive, lower CPU. tightTasks: for cache-friendly tasks, forces periodic yields.
- `new MultiThreadRunner("name", intervalInMs)` — low-CPU runner that ticks at the given interval.
- `Pause()` / `Resume()` — thread-safe; counter stays frozen while paused.
- `WaitForTasksDone(timeoutMs)` — returns `true` if all tasks completed within timeout.
- `Flush()` blocks until reset cleanup completes, rejects task admission during that window, and preserves the worker for reuse.
- `Dispose()` signals terminal reset and joins the worker. Repeated disposal is safe; scheduling afterward throws `MultiThreadRunnerException`.
- `Flush()` and `Dispose()` throw when called from the worker thread, avoiding a self-wait/self-join deadlock.
- Shutdown is cooperative. If a task does not return from `MoveNext()`, cleanup cannot run; `Flush()` or `Dispose()` throws `MultiThreadRunnerException` after two seconds rather than aborting the thread.
- Lifecycle flags are atomic: concurrent stop/reset/kill transitions cannot clear terminal kill state or let the worker sleep before kill cleanup.

### Complete() extension method
```csharp
enumerator.Complete(1000);  // blocks calling thread for up to 1000ms
enumerator.Complete();      // blocks until done (no timeout)
```
- Works on `IEnumerator<TaskContract>` (Lean) and plain `IEnumerator` (ExtraLean).
- Uses a thread-local `SyncRunner` internally — runs synchronously on the calling thread.
- Also works on `SerialTaskCollection` / `ParallelTaskCollection`.

### Flow modifier usage
```csharp
var runner = new SteppableRunner("name");
runner.UseFlowModifier(new SerialFlow());       // one task at a time
runner.UseFlowModifier(new StaggeredFlow(2));   // max 2 tasks per Step()
runner.UseFlowModifier(new TimeBoundFlow(20f)); // 20ms budget per Step()
runner.UseFlowModifier(new TimeSlicedFlow(20f));
```
- `SerialFlow` does NOT guarantee execution order of queued tasks — "tasks removed are shuffled." Do not rely on FIFO ordering with `SerialFlow`.
- `StaggeredFlow(n)` limits to N tasks per `Step()`. If there are more tasks, excess tasks are starved until others complete.
- `TimeBoundFlow(ms)` and `TimeSlicedFlow(ms)` use `Stopwatch` to bound time. TaskCollections count as a single task.

### Task Collections
- **Cannot `Add()` while running** — throws `PreconditionException`.
- `Clear()` removes all tasks. `Reset()` resets collection AND calls `.Reset()` on each task (tasks must support `Reset`; compiler-generated iterators do NOT support `Reset`).
- `ParallelTaskCollection` constructor takes a name and optional capacity: `new ParallelTaskCollection("name", 4)`.
- `SerialTaskCollection` runs tasks sequentially: `[1, -1, 2, -2, 3, -3]`.
- `ParallelTaskCollection` runs tasks concurrently: one `MoveNext` progresses ALL tasks by one step each: `[1, 2]` then `[-1, -2]`.

### Parallel Job Collection (ISveltoJob)
```csharp
struct TestJob : ISveltoJob {
    public int[] results;
    public void Update(int index) { Interlocked.Increment(ref results[index]); }
    public void Dispose() { }
}

var job = new TestJob { results = new int[1024] };
using (var collection = new MultiThreadedParallelJobCollection<TestJob>("test", threadCount: 4, tightTasks: false)) {
    collection.Add(job, 1024);  // 1024 iterations across 4 threads
    collection.Complete(2000);   // ~2s timeout
}
// Every results[i] == 1
```
- Default thread count: `Math.Max(1, Environment.ProcessorCount - 2)`.
- `Add(job, iterations)` splits `iterations` across threads. Remainder is distributed.
- Constructor: `(name, threadCount, tightTasks)`. Worker threads start lazily on the first
  `MoveNext`/`Complete`; `tightTasks: true` forces periodic yields inside worker threads
  so cache-saturating tasks don't starve other threads.
- `onComplete` event fires when all tasks done.
- `Dispose()` disposes ALL added tasks (even if never started).

### MultiThreadedParallelTaskCollection
- Constructor: `(name, threadCount, tightTasks)`.
- `Add(task)` while running throws `MultiThreadedParallelTaskCollectionException`.
- `Stop()` stops execution; `isRunning` becomes false.
- `Reset()` clears tasks, allows reuse.
- 4 tasks that each wait 1 second finish in ~1 second (parallel), not ~4 seconds (serial).

### Iterator Block Pool — the reusable pattern
```csharp
// Define an iterator that loops forever, releasing via Break.It
IEnumerator<TaskContract> MyIterator(PoolData data) {
    while (true) {
        data.value++;
        yield return TaskContract.Break.It;  // signals "release me to pool"
    }
}

var pool = new IteratorBlockPool<PoolData>(MyIterator, "TestPool");
var (data1, block1) = pool.Get();
data1.value = 0;  // MUST initialize data before use
block1.MoveNext();  // data.value = 1
block1.MoveNext();  // data.value = 2, block releases to pool

// Get again — SAME objects recycled!
var (data2, block2) = pool.Get();
Assert.That(data2, Is.SameAs(data1));   // recycled data object
Assert.That(block2, Is.SameAs(block1)); // recycled iterator block
```
- `Break.It` signals the task is done for now, but the state machine stays alive. The pool reclaims the block.
- The data class (`PoolData`) is a **class** (not struct) so its value can change without changing the reference.
- ExtraLean variant: `Svelto.Tasks.ExtraLean.IteratorBlockPool<T>`.
- Both Lean and ExtraLean pools support **concurrent `Get()`/`Return()`** and may be shared across worker runners. `Dispose()` must be called **exactly once**, only after all borrowed blocks have returned and worker activity is quiescent — **concurrent disposal is unsupported**.

### Awaiter / async interop
```csharp
async Task SomeAsyncOperation(SteppableRunner runner) {
    await Task.Delay(10).RunOn(runner);  // Svelto awaiter extension
    continued = true;
    runner.Stop();
    await Task.Delay(10).RunOn(runner);  // this continuation should NOT run after Stop
}
```
- When the runner is stopped, queued continuations do **NOT** execute. `Task.IsCompleted` stays `false`.
- The awaiter posts `async` continuations back onto the Svelto runner via `ContinuationEnumerator`.

### ExtraLean task restrictions
- ExtraLean tasks can yield only: `null`, `Yield.It`, `Break.It`, `Break.AndStop`, or `yield break`.
- Yielding anything else throws `SveltoTaskException` with message: "ExtraLean enumerator can return only null, Yield.It, Break.It, Break.AndStop and yield break".
- The exception faults the task and is reported through `TaskExceptionStrategy.Current`. The built-in strategy logs
  it through `Console.LogException`, which also raises `Console.onException`.

### Compiler-generated iterators and Reset()
- Compiler-generated iterators (from `yield return` methods) do NOT support `Reset()`. Calling `Reset()` throws or does nothing.
- For reusable tasks in collections that call `Reset()`, use custom enumerators like `LeanEnumerator` (which implements `Reset()` manually).
- `SmartFunctionEnumerator<TVal>` is explicitly reusable — tests call `.Continue()` on it multiple times.

### Test helper patterns
The tests use these helper patterns that serve as reference implementations:
- `LeanEnumerator` — basic `IEnumerator<TaskContract>` with `Reset()` support, `AllRight` completion check.
- `ExtraLeanEnumerator` — struct `IEnumerator` with `AllRight` check.
- `WaitEnumerator` — time-based `IParallelTask` for parallel testing.
- `DisposableEnumerator` — tracks `disposed` flag to verify disposal behavior.
- `StartedDisposableEnumerator` — uses `ManualResetEventSlim` to signal task start (for multithreaded tests).
- `StartedDisposableJobEnumerator` — combines `IEnumerator<TaskContract>` and `ISveltoJob` for parallel job disposal tests.
