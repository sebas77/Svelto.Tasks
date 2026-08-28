# Example 04: Preallocated Runner

## Scenario

Your game spawns a known number of concurrent tasks every frame (say 100 AI or
simulation tasks). A `SteppableRunner` starts with internal containers sized for
**3** tasks, so the first big wave forces those buffers to grow several times —
a heap-allocation spike exactly when you don't want one.

## Feature Demonstrated

The optional `initialNumberOfTasks` constructor parameter on
`GenericSteppableRunner` (exposed by `SteppableRunner<T>`), which sizes the runner's
internal task containers (`FasterList`, `TombstoneList`) upfront. The example pairs
`SteppableRunner<WorkTask>` with a struct task so the task stays in its concrete type and
is never boxed into `IEnumerator<TaskContract>`.

## When / Why Use It

- You know the expected number of concurrent tasks (typical in games: pools,
  fixed entity counts) and want to eliminate container-growth allocations.
- You pair a struct enumerator with its matching generic runner (`SteppableRunner<T>`) so
  `RunOn` keeps the concrete struct type and starting a task does not box it.
- Capacity is **retained**: once grown (or preallocated), the buffers survive
  `Flush()`/`Stop()` cycles because the containers' `Clear()` keeps them.

## How It Works

1. Create the runner with the expected concurrency:
   `new SteppableRunner<WorkTask>("RunnerName", TasksPerWave)`
2. The value is forwarded to `SveltoTaskRunner<>.Process`, which sizes
   `_runningCoroutines` (`FasterList<TombstoneHandle>`) and `_spawnedCoroutines`
   (`TombstoneList<(task, handle)>`) at construction.
3. Growth beyond capacity still works — it just allocates (1.5x amortized).

The example measures allocations with `GC.GetAllocatedBytesForCurrentThread()`:

- first wave on a default runner vs. a preallocated one (buffer growth delta)
- steady state: after warm-up, repeated waves allocate zero measured bytes when the runner
  capacity is sufficient, continuations are pooled, and the struct task uses its matching
  generic runner.

## Key Concepts

| Type | Namespace | Role |
|------|-----------|------|
| `SteppableRunner<WorkTask>(name, initialNumberOfTasks)` | `Svelto.Tasks.Lean` | Manual-step runner with upfront capacity and concrete struct storage |
| `WorkTask : IEnumerator<TaskContract>` | example | Struct task — no iterator allocation or interface box per run |
| `TaskContract.Yield.It` | `Svelto.Tasks` | Resume on next `Step()` |
| `GC.GetAllocatedBytesForCurrentThread()` | BCL | Allocation measurement |

## Gotchas

- The parameter sizes the *runner bookkeeping* only. The `_newTaskRoutines`
  queue is a .NET `ConcurrentQueue<T>`, which cannot be preallocated; its
  segments are recycled after the first waves.
- Preallocation removes growth spikes, not per-task iterator allocations.
  Use reusable class enumerators, an `IteratorBlockPool`, or a struct enumerator with its
  matching generic runner. A struct submitted to the non-generic Lean runner is boxed once
  per `RunOn` because that runner stores `IEnumerator<TaskContract>`.

