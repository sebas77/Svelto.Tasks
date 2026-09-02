# Example 13: Batch Pathfinding (MultiThreadedParallelJobCollection + ISveltoJob)

## Scenario

**1000 units** need pathfinding. The work is split across **4 threads** using a
job-based parallel collection — similar to Unity's Jobs system but running on plain .NET.

## Feature Demonstrated

`MultiThreadedParallelJobCollection<TJob>` with a custom `ISveltoJob` struct. The same
job struct is split into **slices** (ranges of indices) and each slice runs on a
separate thread.

## When / Why Use It

- You have a **large number of homogeneous work items** (index-based loops) that can be
  processed independently.
- You want **data-parallelism**: each thread works on a disjoint range of indices.
- The job is a **struct** (no per-iteration allocations), just like Unity Jobs.
- You need the work to **complete synchronously** before continuing (`.Complete()`).

## How It Works

1. Define a `struct` implementing `ISveltoJob`:
   - `void Update(int jobIndex)` — called for each iteration index.
   - `void Dispose()` — called once per thread-slice when done.
2. Create `new MultiThreadedParallelJobCollection<TJob>("name", threadCount, tightTasks)`.
3. Call `collection.Add(job, totalIterations)` — this internally splits the iteration
   range across the threads:
   - `iterations / threadCount` per thread, remainder goes to an extra slice.
4. Call `collection.Complete()` — the `Complete()` extension method runs the collection
   as a synchronous `IEnumerator` until all threads finish.

## Key Concepts

| Type | Namespace | Role |
|------|-----------|------|
| `ISveltoJob` | `Svelto.Tasks.Parallelism` | Interface: `Update(int)`, `Dispose()` — must be a struct |
| `MultiThreadedParallelJobCollection<TJob>` | `Svelto.Tasks.Parallelism.ExtraLean` | Splits job iterations across threads |
| `.Add(job, iterations)` | (on collection) | Registers job with N total iterations |
| `.Complete()` | (extension) | Runs synchronously until all parallel work is done |
| `ParallelRunEnumerator<T>` | `Svelto.Tasks.Parallelism.Internal` | Internal struct that runs a slice of the job |

## Gotchas

- **`TJob` must be a `struct`** that implements `ISveltoJob`. The same struct instance
  is copied into each thread's `ParallelRunEnumerator` — each thread gets its own copy
  of the job's fields.
- **Default thread count** (when using the `(name, tightTasks)` constructor) is
  `Math.Max(1, Environment.ProcessorCount - 2)`. In this example we pass `4` explicitly.
- `Dispose()` is called **once per thread-slice** (not once per job). With 4 threads and
  1000 iterations, `Dispose` will be called 4 times (or 5 if there's a remainder).
- `Update(int)` receives the **global index** (0..999), not a per-thread index. This
  makes it easy to write results into a shared array at the correct position.
- Thread-safety of shared data (e.g., `Interlocked.Increment`) is your responsibility.