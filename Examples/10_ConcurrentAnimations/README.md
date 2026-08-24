# 10 · Concurrent Animations — `ParallelTaskCollection`

## Scenario

Three UI bars — **HP, MP, XP** — must all tween at the same time. Each animation tick
advances **all** bars together, so they fill up in lockstep rather than one after
another.

## Feature

`ParallelTaskCollection` — a `TaskCollection` that progresses **all** tasks on every
`MoveNext`. Tasks are started together and stepped round-robin; each `MoveNext` of the
collection advances every still-running task by one step.

## When / Why to use it

- Concurrent UI animations (health bars, cooldowns, loading splash pieces).
- "Fire all, wait for all" fan-out where tasks should progress in lockstep.
- Any case where overlap is desired and there is no strict ordering between tasks.

## How it works

1. Create the collection:
   ```csharp
   var parallel = new ParallelTaskCollection("UIAnimations");
   ```
2. Add tasks **before** running:
   ```csharp
   parallel.Add(HealthBar());
   parallel.Add(ManaBar());
   parallel.Add(XpBar());
   ```
3. Step it manually with `MoveNext()` (or run with `.Complete(ms)` / `.RunOn(runner)`).
4. `ParallelTaskCollection.RunTasksAndCheckIfDone()` loops over **all** stacks on each
   call. For each task it calls `ProcessStackAndCheckIfDone` once; if that task
   `yieldIt`s, it moves on to the next task in the same pass. A task that finishes
   (`doneIt`) is swapped to the end and `_stackOffset` advances so it's not visited
   again. The collection's own `MoveNext` returns `true` (keep going) until every task
   is done.

### Stepping diagram

```
MoveNext #1:  HP[1] MP[1] XP[1]   ← all advance together
MoveNext #2:  HP[2] MP[2] XP[2]
MoveNext #3:  HP[3] MP[3] XP[3]
   ...
MoveNext #N:  HP[N] MP[N] XP[N]   ← all finish together
```

## Key concepts

| Type / API | Purpose |
|---|---|
| `ParallelTaskCollection` | Concurrent task list; all tasks progress per `MoveNext`. |
| `.Add(IEnumerator<TaskContract>)` | Enqueue a task. Must be called **before** running. |
| `.MoveNext()` | Advance every still-running task by one step. |
| `.Complete(ms)` | Run synchronously to completion (blocks calling thread). |
| `TaskContract.Yield.It` | Suspend this task until the next `MoveNext` pass. |
| `.Reset()` / `.Clear()` | Reset for reuse / remove all. |

## Gotchas

- **All tasks start before any finishes.** Unlike `SerialTaskCollection`, every task
  gets its first `MoveNext` on the very first pass. There is no "wait for the previous
  one" semantics.
- **Cannot `Add()` while running** — same rule as `SerialTaskCollection`. The
  `isRunning` flag is set on the first `MoveNext` and `Add()` throws.
- **Each `MoveNext` advances ALL tasks by one step** (round-robin). If one task does
  heavy synchronous work without yielding, it will block the others until it yields.
  Use `TaskContract.Yield.It` to play nice.
- Finished tasks are **swapped** in the internal array (to skip them on later passes),
  so the order of remaining tasks may shuffle. This is fine for parallel work but
  means you cannot rely on positional ordering across steps.
- `Reset()` requires the task enumerators to support `Reset()`; compiler-generated
  iterators do not. Use custom enumerator classes for reusable collections.