# Example 15: Entity Spawn Pool (IteratorBlockPool)

## Scenario

An entity spawn manager that **pools iterator blocks** instead of allocating new ones
every time an entity spawns. Entities are taken from the pool, run a few steps, then
return themselves via `Break.It`. When a new entity is needed, the pool hands back the
**same recycled block** — zero allocations after warm-up.

## Feature Demonstrated

`IteratorBlockPool<T>` with the `while(true) { yield return TaskContract.Break.It; }`
pattern. The `Break.It` yield returns the block to the pool without destroying the state
machine, enabling reuse.

## When / Why Use It

- You spawn and despawn entities frequently (bullets, particles, minions) and want to
  avoid GC pressure from iterator block allocations.
- The entity's lifecycle is: **spawn → run some steps → despawn** — and it will happen
  again and again.
- The task shape is the **Lean** one (`IEnumerator<TaskContract>`). Blocks can be stepped
  by hand (like this demo) or run on any Lean runner.

## How It Works

1. Define a `Data` class (must be `class, new()` — value types are not allowed because
   the pool needs to swap the data reference without boxing).
2. Write an iterator block that loops forever:
   ```csharp
   IEnumerator<TaskContract> SpawnIterator(EntityData data)
   {
       while (true)
       {
           // do work with data...
           data.Step++;
           yield return TaskContract.Yield.It;  // come back next tick
           // eventually:
           yield return TaskContract.Break.It;  // return to pool, but stay alive!
       }
   }
   ```
3. Create `new IteratorBlockPool<EntityData>(SpawnIterator, "EntityPool")`.
4. Call `pool.Get()` → returns `(EntityData data, PooledIteratorBlock<EntityData> block)`.
5. **Initialize `data`** before use (the pool does not reset it).
6. Call `block.MoveNext()` to step the iterator. When the iterator yields
   `Break.It`, the block auto-returns itself to the pool.
7. Call `pool.Get()` again — you'll get the **same block back** (reference-equal).

## Key Concepts

| Type | Namespace | Role |
|------|-----------|------|
| `IteratorBlockPool<T>` | `Svelto.Tasks.Lean` | Pool of reusable iterator blocks |
| `PooledIteratorBlock<T>` | `Svelto.Tasks.Lean` | Wrapper that auto-returns to pool on `Break.It` |
| `TaskContract.Break.It` | `Svelto.Tasks` | Soft-break: completes current cycle, returns to pool, keeps state machine alive |
| `TaskContract.Yield.It` | `Svelto.Tasks` | Yield one iteration (come back next step) |
| `pool.Get()` | (on pool) | Get a (data, block) tuple from the pool |
| `pool.Dispose()` | (on pool) | Clean up all pooled blocks |

## Gotchas

- **`Break.It` keeps the state machine alive** for reuse. `yield break` destroys it — the
  block will NOT be recycled. Always use `Break.It` in the `while(true)` pattern.
- **`Data` must be a `class`** (reference type). The pool stores the data reference and
  swaps its fields between uses. If `Data` were a struct, field changes would be lost.
- **Must initialize `Data` before use after `Get()`.** The pool does not reset the data
  object — you get it back with whatever state it had when it was last returned. Always
  set `data.X = initialValue` right after `Get()`.
- The `PooledIteratorBlock<T>.MoveNext()` method checks if the current value is
  `Break.It` (or `Break.AndStop`). If so, it calls `pool.Return(data, this)` and returns
  `false`. The next `Get()` will pop this same block from the stack.
- The pool uses a `Stack` internally — it's LIFO. The most recently returned block is the
  first to be reused.