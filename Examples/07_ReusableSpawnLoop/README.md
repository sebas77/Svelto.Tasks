# 07 · Reusable Spawn Loop — `Break.It` + `IteratorBlockPool<T>`

## Scenario

An enemy spawn manager that **reuses the same iterator block** every wave instead of
allocating a new compiler-generated `IEnumerator` each time. The block is recycled
through a pool thanks to the `while (true) { ...; yield return TaskContract.Break.It; }`
pattern.

## Feature

- **`IteratorBlockPool<P>`** — a pool that hands out `PooledIteratorBlock<P>`
  wrappers around a single iterator-block factory.
- **`PooledIteratorBlock<P>`** — an `IEnumerator<TaskContract>` that, when it sees
  `Break.It` (or `Break.AndStop`), returns itself to the pool instead of dying.
- **`TaskContract.Break.It`** — a special yield that ends the *current* iteration but
  keeps the state machine alive (unlike `yield break` which destroys it).

## When / Why to use it

- Hot paths that would otherwise allocate a new iterator every frame/wave (spawners,
  AI ticks, repeated tween loops).
- When you want deterministic, zero-GC iteration reuse.
- When the same logic must run again and again with fresh `Data` each time.

## How it works

1. Create the pool:
   ```csharp
   var pool = new IteratorBlockPool<SpawnData>(SpawnIterator, "EnemySpawnPool");
   ```
   The factory `Func<SpawnData, IEnumerator<TaskContract>>` is called **once** per
   pooled block (not per Get).
2. `pool.Get()` returns `(SpawnData data, PooledIteratorBlock<SpawnData> block)`.
   On the first call it allocates one block; afterwards it reuses the pooled one.
3. Run the block with `MoveNext()`. The iterator does its work, `yield return
   TaskContract.Yield.It` to suspend, then `yield return TaskContract.Break.It`.
4. `PooledIteratorBlock.MoveNext()` detects `breakMode.AnyBreak`, calls
   `pool.Return(data, this)` and returns `false`. The block is back in the pool.
5. Next `pool.Get()` returns the **same** `PooledIteratorBlock` instance (same hash
   code) because the underlying `while(true)` state machine never ended.

### Recycling diagram

```
pool.Get() ─▶ Spawn ─▶ yield Break.It
    ▲                     │
    │                     ▼
    └──── pool.Return() ◀┘
(state machine stays alive and gets reused)
```

## Key concepts

| Type / API | Purpose |
|---|---|
| `IteratorBlockPool<P>` | Pool of reusable iterator blocks. `P` must be a **class** with `new()`. |
| `PooledIteratorBlock<P>` | The pooled `IEnumerator<TaskContract>` wrapper. |
| `TaskContract.Break.It` | End this iteration, keep the state machine alive for reuse. |
| `while (true)` | Required so the state machine never truly finishes. |
| `pool.Get()` / `pool.Return()` | Checkout / checkin (Return is called automatically by the wrapper). |
| `pool.Dispose()` | Disposes all pooled blocks. |

## Gotchas

- **`Data` must be a `class`, not a `struct`.** The constraint is `where P : class, new()`.
  The pool reuses the same `P` instance and mutates its fields between cycles.
- **`Break.It` keeps the state machine alive.** `yield break` destroys it — once you
  `yield break` the block is dead and cannot be reused.
- Always **re-initialise `Data`** after `pool.Get()` before running, since it may
  carry state from the previous cycle.
- `PooledIteratorBlock.Reset()` throws `NotImplementedException` — do not call it.
- The `while(true)` loop must `yield return TaskContract.Break.It` to exit each cycle;
  without it the block would run forever and never return to the pool.